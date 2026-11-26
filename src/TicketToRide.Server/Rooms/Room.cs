using System.Threading.Channels;
using TicketToRide.Shared;
using TicketToRide.Shared.Net;

namespace TicketToRide.Server;

/// <summary>Trạng thái nội bộ (Mục 3). Trên đường truyền chỉ có LOBBY, IN_GAME, ENDED.</summary>
public enum RoomStatus { Lobby, InGame, Ended, Recovering, Closed }

public sealed class Member
{
    public Member(Session session) => Session = session;
    public Session Session { get; }
    public bool Ready { get; set; }
}

/// <summary>Giao diện kết nối tới GameLogic (do thành viên phụ trách luật chơi cài đặt).</summary>
public interface IGameEngine
{
    int StateVersion { get; }
    string CurrentTurnPlayerId { get; }
    bool IsSetupTicketsPhase { get; }
    object CreateSnapshot();
    void AdvanceTurnSkippingLeftMatch(IReadOnlyList<Member> members);
    (bool Ok, string ErrorCode, string ErrorText, object? StateDelta, bool GameEnded) ApplyAction(
        string playerId, Message msg, IReadOnlyList<Member> members);
}

/// <summary>Giao diện kết nối tới Persistence (lưu SQL Server cùng transaction với reqId - Mục 5.3).</summary>
public interface IMatchPersistence
{
    Task SaveMatchStepAsync(string roomCode, int stateVersion, string playerId, string? reqId, string cachedReply, object snapshot);
}

public sealed class Room
{
    const int InGameGraceSeconds = 60;       // Mục 1 & 6.1: chờ lượt / ân hạn trong trận 60s
    const int RecoveryConfirmSeconds = 60;   // Mục 3 & 4: xác nhận khôi phục 60s
    const int EndedRetentionSeconds = 600;   // Mục 3 & 7.2: giữ phòng ENDED 10 phút (600s)

    enum TimerKind { LobbyGrace, InGameGrace, RecoveryConfirm, EndedCleanup }
    readonly record struct ActiveTimer(int Seq, TimerKind Kind);

    readonly SessionManager _sessions;
    readonly RoomManager _manager;
    readonly IGameEngine? _engine;
    readonly IMatchPersistence? _persistence;
    readonly SqlMatchPersistence? _sqlPersistence;
    readonly bool _isMatchReadyCheck;
    readonly bool _isRankedMatch;
    readonly MatchmakingService? _matchmaking;
    readonly List<Member> _members = new();
    readonly Dictionary<(string PlayerId, string ReqId), string> _processed = new();
    readonly Dictionary<string, Queue<string>> _processedOrder = new();
    readonly Dictionary<string, ActiveTimer> _timers = new();
    Guid? _dbRoomId;
    Guid? _dbMatchId;
    int _seqCounter;
    int _endedTimerSeq;
    int _readyCheckRemaining;
    int _readyCheckSeq;
    bool _readyCheckStarted;

    public Room(
        string code,
        Session host,
        SessionManager sessions,
        RoomManager manager,
        IGameEngine? engine = null,
        IMatchPersistence? persistence = null,
        bool isMatchReadyCheck = false,
        bool isRankedMatch = false,
        MatchmakingService? matchmaking = null)
    {
        Code = code;
        HostId = host.PlayerId;
        _sessions = sessions;
        _manager = manager;
        _engine = engine;
        _persistence = persistence;
        _sqlPersistence = persistence as SqlMatchPersistence;
        _isMatchReadyCheck = isMatchReadyCheck;
        _isRankedMatch = isRankedMatch;
        _matchmaking = matchmaking;
        _members.Add(new Member(host));
    }

    public string Code { get; }
    public string HostId { get; private set; }
    public RoomStatus Status { get; private set; } = RoomStatus.Lobby;

    public string WireRoomStatus => Status switch
    {
        RoomStatus.Lobby => WireStatus.Lobby,
        RoomStatus.Ended or RoomStatus.Closed => WireStatus.Ended,
        _ => WireStatus.InGame,
    };

    public Channel<RoomCommand> Inbox { get; } =
        Channel.CreateUnbounded<RoomCommand>(new UnboundedChannelOptions { SingleReader = true });

    public Task RunAsync() => Task.Run(async () =>
    {
        await foreach (var cmd in Inbox.Reader.ReadAllAsync())
        {
            try { await HandleAsync(cmd); }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[Room {Code}] {ex}");
                if (cmd is ClientMessage c)
                    SendError(c.Session, c.Msg, ErrorCodes.InternalError, "Lỗi nội bộ của server");
            }
        }
    });

    // ------------------------------------------------------------ Điều phối lệnh tuần tự

    async Task HandleAsync(RoomCommand cmd)
    {
        switch (cmd)
        {
            case ClientMessage c: await OnClientMessageAsync(c); break;
            case ConnectionLost l: await OnConnectionLostAsync(l.PlayerId); break;
            case ConnectionRestored r: OnConnectionRestored(r.PlayerId); break;
            case MemberLoggedOut o: await OnMemberLoggedOutAsync(o.PlayerId); break;
            case TimerFired t: await OnTimerFiredAsync(t); break;
            case RecoveryConfirm rc: await OnRecoveryConfirmAsync(rc); break;
        }
    }

    async Task OnClientMessageAsync(ClientMessage c)
    {
        var s = c.Session;
        var msg = c.Msg;

        // Mục 5.3 Bước 1: Tra Processed theo (playerId, reqId)
        if (msg.ReqId != null && _processed.TryGetValue((s.PlayerId, msg.ReqId), out var cached))
        {
            s.Conn?.Enqueue(cached);
            return;
        }

        // Mục 3: Đã gửi GAME_ENDED -> hành động mới nhận lỗi GAME_ENDED (trừ LEAVE_ROOM / SYNC_REQUEST)
        if (Status == RoomStatus.Ended && msg.Type is not (MessageTypes.LeaveRoom or "SYNC_REQUEST"))
        {
            SendError(s, msg, "GAME_ENDED", "Trận đấu đã kết thúc");
            return;
        }

        // Mục 3 & 4: Đang RECOVERING -> không nhận hành động làm đổi ván, chỉ nhận xác nhận khôi phục
        if (Status == RoomStatus.Recovering && msg.Type is not ("RECOVERY_CONFIRM" or MessageTypes.LeaveRoom))
        {
            SendError(s, msg, "RECOVERING", "Server đang khôi phục ván, vui lòng xác nhận tiếp tục");
            return;
        }

        switch (msg.Type)
        {
            case MessageTypes.CreateRoom:
                await OnCreateRoomAsync(s, msg);
                break;
            case MessageTypes.JoinRoom:
                await OnJoinAsync(s, msg);
                break;
            case MessageTypes.SetReady:
                await OnSetReadyAsync(s, msg);
                break;
            case MessageTypes.StartGame:
                await OnStartGameAsync(s, msg);
                break;
            case MessageTypes.LeaveRoom:
                await OnLeaveAsync(s, msg);
                break;
            case "SYNC_REQUEST":
                OnSyncRequest(s, msg);
                break;
            case "RECOVERY_CONFIRM":
                await OnRecoveryConfirmAsync(new RecoveryConfirm(s.PlayerId, true), s, msg);
                break;
            default:
                // Các hành động chơi trong trận (DRAW_CARD, CLAIM_ROUTE, KEEP_TICKETS...)
                await OnGameActionAsync(s, msg);
                break;
        }
    }

    // ------------------------------------------------------------ Phòng chờ (LOBBY) & ReadyCheck ghép trận

    async Task OnCreateRoomAsync(Session host, Message msg)
    {
        if (host.Conn != null)
            host.State = ConnState.Connected;

        var hostMember = Find(host.PlayerId);
        if (hostMember != null)
            hostMember.Ready = false;

        if (_sqlPersistence != null)
        {
            try
            {
                _dbRoomId = await _sqlPersistence.EnsureRoomCreatedAsync(Code, host, (byte)Protocol.MaxPlayers);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[Room {Code}] Lỗi lưu CreateRoom vào SQL Server: {ex.Message}");
            }
        }

        SendAck(host, msg, new AckPayload(Code));

        if (_isMatchReadyCheck && Status == RoomStatus.Lobby)
        {
            EnsureReadyCheckStarted();
            BroadcastReadyCheck();
        }
        else
        {
            Broadcast(Message.Create(MessageTypes.RoomUpdated, BuildUpdate()));
        }
    }

    async Task OnJoinAsync(Session s, Message msg)
    {
        if (Status != RoomStatus.Lobby) { SendError(s, msg, ErrorCodes.RoomInGame, "Phòng đã bắt đầu ván"); return; }
        if (_members.Count >= Protocol.MaxPlayers) { SendError(s, msg, ErrorCodes.RoomFull, "Phòng đã đầy"); return; }
        if (_members.Any(m => string.Equals(m.Session.PlayerName, s.PlayerName, StringComparison.OrdinalIgnoreCase)))
        { SendError(s, msg, ErrorCodes.NameTaken, "Tên người chơi đã có trong phòng"); return; }

        if (s.Conn != null)
            s.State = ConnState.Connected;

        var member = new Member(s) { Ready = false };
        _members.Add(member);
        _manager.Bind(s, Code);

        if (_sqlPersistence != null)
        {
            try
            {
                _dbRoomId ??= await _sqlPersistence.EnsureRoomCreatedAsync(Code, _members[0].Session, (byte)Protocol.MaxPlayers);
                await _sqlPersistence.SyncRoomPlayerAsync(_dbRoomId, Code, s, "Joined");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[Room {Code}] Lỗi lưu JoinRoom vào SQL Server: {ex.Message}");
            }
        }

        SendAck(s, msg, new AckPayload(Code));

        if (_isMatchReadyCheck && Status == RoomStatus.Lobby)
        {
            EnsureReadyCheckStarted();
            BroadcastReadyCheck();
        }
        else
        {
            Broadcast(Message.Create(MessageTypes.RoomUpdated, BuildUpdate()));
        }
    }

    void EnsureReadyCheckStarted()
    {
        if (_readyCheckStarted) return;
        _readyCheckStarted = true;
        _readyCheckRemaining = Protocol.ReadyCheckSeconds;
        var seq = ++_readyCheckSeq;

        _ = Task.Run(async () =>
        {
            while (true)
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
                Inbox.Writer.TryWrite(new TimerFired(seq, "__READY_CHECK__"));
            }
        });
    }

    void BroadcastReadyCheck()
    {
        var payload = new ReadyCheckUpdatedPayload(
            Code,
            _isRankedMatch,
            Math.Max(0, _readyCheckRemaining),
            BuildPlayersPublic());
        Broadcast(Message.Create(MessageTypes.ReadyCheckUpdated, payload));
    }

    async Task OnSetReadyAsync(Session s, Message msg)
    {
        var m = Find(s.PlayerId);
        if (m == null) { SendError(s, msg, ErrorCodes.NotInRoom, "Bạn không ở trong phòng"); return; }
        if (Status != RoomStatus.Lobby) { SendError(s, msg, ErrorCodes.RoomInGame, "Phòng đã bắt đầu ván"); return; }

        var p = msg.ReadPayload<SetReadyPayload>();
        if (p == null) { SendError(s, msg, ErrorCodes.BadPayload, "Thiếu trường ready"); return; }

        m.Ready = p.Ready;

        if (_isMatchReadyCheck)
        {
            if (_members.Count >= Protocol.MinPlayers &&
                _members.All(x => x.Ready && x.Session.State == ConnState.Connected))
            {
                _readyCheckSeq++;
                _matchmaking?.ClearSavedElapsed(_members.Select(x => x.Session.PlayerId));
                SendAck(s, msg, null);
                await StartMatchInternalAsync();
                return;
            }

            SendAck(s, msg, null);
            BroadcastReadyCheck();
            return;
        }

        AckAndBroadcastRoom(s, msg, null);
    }

    async Task OnStartGameAsync(Session s, Message msg)
    {
        if (Find(s.PlayerId) == null) { SendError(s, msg, ErrorCodes.NotInRoom, "Bạn không ở trong phòng"); return; }
        if (Status != RoomStatus.Lobby) { SendError(s, msg, ErrorCodes.RoomInGame, "Phòng đã bắt đầu ván"); return; }
        if (s.PlayerId != HostId) { SendError(s, msg, ErrorCodes.NotHost, "Chỉ chủ phòng được bắt đầu"); return; }
        if (_members.Count < Protocol.MinPlayers || _members.Count > Protocol.MaxPlayers)
        { SendError(s, msg, ErrorCodes.NotEnoughPlayers, "Cần 2–4 người chơi"); return; }
        if (_members.Any(m => m.Session.PlayerId != HostId && (!m.Ready || m.Session.State != ConnState.Connected)))
        { SendError(s, msg, ErrorCodes.NotAllReady, "Chưa phải ai cũng sẵn sàng"); return; }

        SendAck(s, msg, null);
        await StartMatchInternalAsync();
    }

    async Task StartMatchInternalAsync()
    {
        Status = RoomStatus.InGame;
        var snap = _engine?.CreateSnapshot();

        if (_sqlPersistence != null)
        {
            try
            {
                _dbMatchId = await _sqlPersistence.StartMatchAsync(_dbRoomId, Code, _members, snap);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[Room {Code}] Lỗi lưu StartGame vào SQL Server: {ex.Message}");
            }
        }

        Broadcast(Message.Create(MessageTypes.RoomUpdated, BuildUpdate()));

        if (_engine != null && snap != null)
            Broadcast(Message.Create("GAME_SNAPSHOT", snap));
    }

    async Task ResolveReadyCheckTimeoutAsync()
    {
        if (Status != RoomStatus.Lobby || !_isMatchReadyCheck) return;

        if (_members.Count >= Protocol.MinPlayers &&
            _members.All(x => x.Ready && x.Session.State == ConnState.Connected))
        {
            _matchmaking?.ClearSavedElapsed(_members.Select(x => x.Session.PlayerId));
            await StartMatchInternalAsync();
            return;
        }

        var readyMembers = _members.Where(x => x.Ready && x.Session.State != ConnState.LeftMatch).ToList();
        var notReadyMembers = _members.Where(x => !x.Ready && x.Session.State != ConnState.LeftMatch).ToList();

        foreach (var bad in notReadyMembers)
        {
            _matchmaking?.BanPlayer(bad.Session.PlayerId, Protocol.MatchPenaltySeconds);
            bad.Session.Conn?.Enqueue(Message.Create(
                MessageTypes.MatchPenalty,
                new MatchPenaltyPayload(
                    Protocol.MatchPenaltySeconds,
                    $"Bạn chưa sẵn sàng nên bị cấm ghép trận Rank/Thường trong {Protocol.MatchPenaltySeconds} giây.")).ToJson());
            RemoveMember(bad);
        }

        foreach (var good in readyMembers)
        {
            RemoveMember(good);
            _matchmaking?.RequeueAfterReadyCheck(good.Session, _isRankedMatch);
        }

        await CloseAsync("Closed");
    }

    // ------------------------------------------------------------ Xử lý hành động chơi (Mục 5.3)

    async Task OnGameActionAsync(Session s, Message msg)
    {
        var m = Find(s.PlayerId);
        if (m == null) { SendError(s, msg, ErrorCodes.NotInRoom, "Bạn không ở trong phòng"); return; }
        if (Status != RoomStatus.InGame) { SendError(s, msg, ErrorCodes.UnknownType, $"Phòng không xử lý thông điệp {msg.Type}"); return; }

        // Mục 4: Chỉ trạng thái CONNECTED mới được gửi hành động chơi
        if (s.State != ConnState.Connected)
        {
            SendError(s, msg, "INVALID_STATE", "Trạng thái kết nối hiện tại không thể thực hiện hành động");
            return;
        }

        if (_engine == null)
        {
            SendError(s, msg, ErrorCodes.InternalError, "GameEngine chưa được gắn vào phòng");
            return;
        }

        // Bước 2 & 3: Kiểm tra luật & tính trạng thái mới trên bản nháp
        var (ok, errCode, errText, stateDelta, gameEnded) = _engine.ApplyAction(s.PlayerId, msg, _members);
        if (!ok)
        {
            SendError(s, msg, errCode, errText);
            return;
        }

        var ackReply = Message.Create(MessageTypes.Ack, null, msg.ReqId).ToJson();
        var nextSnapshot = _engine.CreateSnapshot();

        // Bước 4: Ghi SQL Server trong một transaction cùng với reqId
        if (_sqlPersistence != null && _dbMatchId != null)
        {
            await _sqlPersistence.SaveMatchStepWithMatchIdAsync(
                _dbMatchId,
                s.DbAccountId,
                msg.Type,
                _engine.StateVersion,
                msg.ReqId,
                true,
                ackReply,
                nextSnapshot);
        }
        else if (_persistence != null)
        {
            await _persistence.SaveMatchStepAsync(Code, _engine.StateVersion, s.PlayerId, msg.ReqId, ackReply, nextSnapshot);
        }

        // Bước 5: Commit xong mới lưu cache reqId, gửi ACK và phát STATE_UPDATE
        if (msg.ReqId != null) Remember(s.PlayerId, msg.ReqId, ackReply);
        s.Conn?.Enqueue(ackReply);

        if (stateDelta != null)
            Broadcast(Message.Create("STATE_UPDATE", stateDelta));

        if (gameEnded)
            await TransitionToEndedAsync();
    }

    void OnSyncRequest(Session s, Message msg)
    {
        if (Find(s.PlayerId) == null) { SendError(s, msg, ErrorCodes.NotInRoom, "Bạn không ở trong phòng"); return; }
        if (_engine != null)
            s.Conn?.Enqueue(Message.Create("GAME_SNAPSHOT", _engine.CreateSnapshot(), msg.ReqId).ToJson());
        else
            s.Conn?.Enqueue(Message.Create(MessageTypes.RoomUpdated, BuildUpdate(), msg.ReqId).ToJson());
    }

    // ------------------------------------------------------------ Rời phòng & LEFT_MATCH (Mục 6)

    async Task OnLeaveAsync(Session s, Message msg)
    {
        var m = Find(s.PlayerId);
        if (m == null) { SendError(s, msg, ErrorCodes.NotInRoom, "Bạn không ở trong phòng"); return; }

        // Mục 6.1: Rời khi đang LOBBY hoặc ENDED -> gỡ khỏi phòng
        if (Status is RoomStatus.Lobby or RoomStatus.Ended)
        {
            if (_sqlPersistence != null && Status == RoomStatus.Lobby)
            {
                try { await _sqlPersistence.SyncRoomPlayerAsync(_dbRoomId, Code, s, "Left"); }
                catch { }
            }

            RemoveMember(m);
            SendAck(s, msg, null);
            await AfterMemberRemovedAsync();
            return;
        }

        // Mục 6 & 6.1: Gửi LEAVE_ROOM khi đang IN_GAME / RECOVERING -> chuyển thành LEFT_MATCH
        SendAck(s, msg, null);
        await TransitionMemberToLeftMatchAsync(m);
    }

    /// <summary>Thứ tự 4 bước chuẩn khi chuyển sang LEFT_MATCH (Mục 6 & 6.2).</summary>
    async Task TransitionMemberToLeftMatchAsync(Member m)
    {
        var s = m.Session;
        if (s.State == ConnState.LeftMatch) return;

        // 1. Đặt trạng thái LEFT_MATCH và thu hồi resumeToken
        _timers.Remove(s.PlayerId);
        s.State = ConnState.LeftMatch;
        _manager.Unbind(s);
        _sessions.Remove(s, $"rời trận tại phòng {Code} (LEFT_MATCH)");

        if (_sqlPersistence != null)
        {
            try { await _sqlPersistence.SyncRoomPlayerAsync(_dbRoomId, Code, s, "Left"); }
            catch { }
        }

        // 2. Nếu đang là lượt của người đó, chuyển turn ngay cho người kế tiếp chưa LEFT_MATCH
        bool turnChanged = false;
        if (_engine != null && !_engine.IsSetupTicketsPhase && _engine.CurrentTurnPlayerId == s.PlayerId)
        {
            _engine.AdvanceTurnSkippingLeftMatch(_members);
            turnChanged = true;
        }

        // 3. Phát sự kiện PLAYER_CONNECTION với status = 'LEFT_MATCH', kèm STATE_UPDATE chuyển lượt
        BroadcastConnection(s);
        if (turnChanged && _engine != null)
            Broadcast(Message.Create("STATE_UPDATE", _engine.CreateSnapshot()));

        // Nếu tất cả người chơi đều đã LEFT_MATCH thì đóng phòng
        if (_members.All(x => x.Session.State == ConnState.LeftMatch))
            await CloseAsync("Abandoned");
    }

    // ------------------------------------------------------------ Mất kết nối / Nối lại / Khôi phục (Mục 3, 4, 6.1)

    async Task OnConnectionLostAsync(string playerId)
    {
        var m = Find(playerId);
        if (m == null) return;
        var s = m.Session;
        if (s.Conn != null) return;
        if (s.State == ConnState.LeftMatch) return;

        // Mục 6.1: Mất kết nối khi ENDED -> gỡ khỏi danh sách theo dõi
        if (Status == RoomStatus.Ended)
        {
            RemoveMember(m);
            _sessions.Remove(s, $"ngắt kết nối sau khi ván {Code} kết thúc");
            await AfterMemberRemovedAsync();
            return;
        }

        s.State = ConnState.Disconnected;

        if (Status == RoomStatus.Lobby)
        {
            StartPlayerTimer(playerId, TimerKind.LobbyGrace, Protocol.LobbyGraceSeconds);
        }
        else if (Status == RoomStatus.InGame)
        {
            // Mục 6.1: Mất kết nối trong IN_GAME -> DISCONNECTED, chạy đồng hồ ân hạn 60s
            StartPlayerTimer(playerId, TimerKind.InGameGrace, InGameGraceSeconds);
        }

        BroadcastConnection(s);
    }

    void OnConnectionRestored(string playerId)
    {
        var m = Find(playerId);
        if (m == null) return;
        var s = m.Session;
        if (s.State == ConnState.LeftMatch) return;

        _timers.Remove(playerId);

        if (Status == RoomStatus.Recovering)
        {
            s.State = ConnState.AwaitingConfirm;
            s.Conn?.Enqueue(Message.Create(MessageTypes.RoomUpdated, BuildUpdate()).ToJson());
            BroadcastConnection(s);
            return;
        }

        s.State = ConnState.Connected;

        if (Status == RoomStatus.Lobby || _engine == null)
            s.Conn?.Enqueue(Message.Create(MessageTypes.RoomUpdated, BuildUpdate()).ToJson());
        else
            s.Conn?.Enqueue(Message.Create("GAME_SNAPSHOT", _engine.CreateSnapshot()).ToJson());

        BroadcastConnection(s);
    }

    public void SendCurrentStateTo(Session s)
    {
        var m = Find(s.PlayerId);
        if (m == null || m.Session.State == ConnState.LeftMatch) return;

        _timers.Remove(s.PlayerId);
        s.State = ConnState.Connected;

        if (Status == RoomStatus.Lobby || _engine == null)
            s.Conn?.Enqueue(Message.Create(MessageTypes.RoomUpdated, BuildUpdate()).ToJson());
        else
            s.Conn?.Enqueue(Message.Create("GAME_SNAPSHOT", _engine.CreateSnapshot()).ToJson());

        BroadcastConnection(s);
    }

    async Task OnMemberLoggedOutAsync(string playerId)
    {
        var m = Find(playerId);
        if (m == null) return;

        _timers.Remove(playerId);
        if (Status is RoomStatus.Lobby or RoomStatus.Ended)
        {
            RemoveMember(m);
            await AfterMemberRemovedAsync();
        }
        else
        {
            await TransitionMemberToLeftMatchAsync(m);
        }
    }

    /// <summary>Kích hoạt trạng thái RECOVERING sau khi server khởi động lại từ SQL Server (Mục 3 & 4).</summary>
    public void EnterRecoveryMode()
    {
        Status = RoomStatus.Recovering;
        foreach (var m in _members)
        {
            if (m.Session.State == ConnState.LeftMatch) continue;
            m.Session.State = ConnState.AwaitingConfirm;
            StartPlayerTimer(m.Session.PlayerId, TimerKind.RecoveryConfirm, RecoveryConfirmSeconds);
        }
    }

    async Task OnRecoveryConfirmAsync(RecoveryConfirm rc, Session? s = null, Message? req = null)
    {
        if (Status != RoomStatus.Recovering) return;
        var m = Find(rc.PlayerId);
        if (m == null) return;

        if (!rc.Accept)
        {
            if (s != null && req != null) SendAck(s, req, null);
            await TransitionMemberToLeftMatchAsync(m);
        }
        else
        {
            _timers.Remove(rc.PlayerId);
            m.Session.State = ConnState.Connected;
            if (s != null && req != null) SendAck(s, req, null);
            BroadcastConnection(m.Session);
        }

        // Khi không còn ai ở trạng thái AwaitingConfirm -> quay lại IN_GAME
        if (_members.All(x => x.Session.State != ConnState.AwaitingConfirm))
        {
            Status = RoomStatus.InGame;
            Broadcast(Message.Create(MessageTypes.RoomUpdated, BuildUpdate()));
            if (_engine != null)
                Broadcast(Message.Create("GAME_SNAPSHOT", _engine.CreateSnapshot()));
        }
    }

    async Task TransitionToEndedAsync()
    {
        if (Status == RoomStatus.Ended) return;
        Status = RoomStatus.Ended;
        _timers.Clear();

        if (_sqlPersistence != null)
        {
            try { await _sqlPersistence.CompleteMatchAsync(_dbMatchId, _dbRoomId, Code, "Completed", "Closed"); }
            catch { }
        }

        Broadcast(Message.Create("GAME_ENDED", _engine?.CreateSnapshot()));
        Broadcast(Message.Create(MessageTypes.RoomUpdated, BuildUpdate()));

        // Mục 3 & 7.2: Giữ phòng ENDED tối đa 10 phút (600s) trước khi CLOSED
        var seq = ++_endedTimerSeq;
        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(EndedRetentionSeconds));
            Inbox.Writer.TryWrite(new TimerFired(seq, "__ENDED__"));
        });
    }

    async Task OnTimerFiredAsync(TimerFired t)
    {
        if (t.PlayerId == "__READY_CHECK__")
        {
            if (Status != RoomStatus.Lobby || !_isMatchReadyCheck || t.Seq != _readyCheckSeq) return;
            _readyCheckRemaining--;
            if (_readyCheckRemaining > 0)
            {
                BroadcastReadyCheck();
            }
            else
            {
                await ResolveReadyCheckTimeoutAsync();
            }
            return;
        }

        if (t.PlayerId == "__ENDED__")
        {
            if (Status == RoomStatus.Ended && t.Seq == _endedTimerSeq) await CloseAsync("Closed");
            return;
        }

        if (!_timers.TryGetValue(t.PlayerId, out var active) || active.Seq != t.Seq) return;
        _timers.Remove(t.PlayerId);

        var m = Find(t.PlayerId);
        if (m == null) return;
        var s = m.Session;

        switch (active.Kind)
        {
            case TimerKind.LobbyGrace:
                if (s.State != ConnState.Disconnected) return;
                RemoveMember(m);
                _sessions.Remove(s, $"hết ân hạn {Protocol.LobbyGraceSeconds}s ở phòng {Code}");
                await AfterMemberRemovedAsync();
                break;

            case TimerKind.InGameGrace:
                // Mục 6.1: Hết ân hạn 60s trong trận -> chuyển LEFT_MATCH, thu hồi token & chuyển lượt
                if (s.State != ConnState.Disconnected) return;
                await TransitionMemberToLeftMatchAsync(m);
                break;

            case TimerKind.RecoveryConfirm:
                // Mục 3: Hết 60s xác nhận khôi phục -> loại người không xác nhận (LEFT_MATCH) và quay về IN_GAME
                if (s.State != ConnState.AwaitingConfirm) return;
                await TransitionMemberToLeftMatchAsync(m);
                if (Status == RoomStatus.Recovering && _members.All(x => x.Session.State != ConnState.AwaitingConfirm))
                {
                    Status = RoomStatus.InGame;
                    Broadcast(Message.Create(MessageTypes.RoomUpdated, BuildUpdate()));
                }
                break;
        }
    }

    void StartPlayerTimer(string playerId, TimerKind kind, int seconds)
    {
        var seq = ++_seqCounter;
        _timers[playerId] = new ActiveTimer(seq, kind);
        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(seconds));
            Inbox.Writer.TryWrite(new TimerFired(seq, playerId));
        });
    }

    // ------------------------------------------------------------ Quản lý thành viên & Gửi gói tin

    Member? Find(string playerId) => _members.FirstOrDefault(m => m.Session.PlayerId == playerId);

    void RemoveMember(Member m)
    {
        _members.Remove(m);
        _timers.Remove(m.Session.PlayerId);
        _manager.Unbind(m.Session);
        if (m.Session.Conn != null && m.Session.State != ConnState.LeftMatch)
            m.Session.State = ConnState.Connected;
    }

    async Task AfterMemberRemovedAsync()
    {
        if (_members.Count == 0) { await CloseAsync("Closed"); return; }

        if (_members.All(m => m.Session.PlayerId != HostId))
        {
            var next = _members.FirstOrDefault(m => m.Session.State == ConnState.Connected) ?? _members[0];
            HostId = next.Session.PlayerId;
        }
        Broadcast(Message.Create(MessageTypes.RoomUpdated, BuildUpdate()));
    }

    async Task CloseAsync(string matchStatus = "Closed")
    {
        if (Status == RoomStatus.Closed) return;
        Status = RoomStatus.Closed;
        _readyCheckSeq++;

        if (_sqlPersistence != null && (_dbMatchId != null || _dbRoomId != null))
        {
            try
            {
                var effectiveMatchStatus = _dbMatchId != null
                    ? (matchStatus == "Abandoned" ? "Abandoned" : "Completed")
                    : "Completed";
                await _sqlPersistence.CompleteMatchAsync(_dbMatchId, _dbRoomId, Code, effectiveMatchStatus, "Closed");
            }
            catch { }
        }

        foreach (var m in _members.ToList())
        {
            _manager.Unbind(m.Session);
            if (m.Session.Conn == null) _sessions.Remove(m.Session, $"phòng {Code} đã đóng");
        }
        _members.Clear();
        _timers.Clear();
        _manager.Remove(Code);
        Inbox.Writer.TryComplete();
    }

    List<PlayerPublic> BuildPlayersPublic() =>
        _members.Select(m => new PlayerPublic(
            m.Session.PlayerId,
            m.Session.PlayerName,
            ConnStatus(m.Session.State),
            m.Ready,
            m.Session.PlayerId == HostId)).ToList();

    RoomUpdatedPayload BuildUpdate() => new(
        Code,
        Status switch
        {
            RoomStatus.Lobby => WireStatus.Lobby,
            RoomStatus.Ended or RoomStatus.Closed => WireStatus.Ended,
            _ => WireStatus.InGame, // IN_GAME và RECOVERING trên đường truyền là IN_GAME (Mục 1 & 3)
        },
        HostId,
        BuildPlayersPublic());

    static string ConnStatus(ConnState st) => st switch
    {
        ConnState.Connected => WireStatus.Connected,
        ConnState.LeftMatch => WireStatus.LeftMatch,
        _ => WireStatus.Disconnected, // DISCONNECTED và AWAITING_CONFIRM đều map ra wire status chuẩn
    };

    void Broadcast(Message m)
    {
        var json = m.ToJson();
        foreach (var mem in _members) mem.Session.Conn?.Enqueue(json);
    }

    void BroadcastConnection(Session s) =>
        Broadcast(Message.Create(MessageTypes.PlayerConnection,
            new PlayerConnectionPayload(s.PlayerId, ConnStatus(s.State))));

    void AckAndBroadcastRoom(Session s, Message req, object? ackPayload)
    {
        SendAck(s, req, ackPayload);
        Broadcast(Message.Create(MessageTypes.RoomUpdated, BuildUpdate()));
    }

    void SendAck(Session s, Message req, object? payload)
    {
        var reply = Message.Create(MessageTypes.Ack, payload, req.ReqId).ToJson();
        if (req.ReqId != null) Remember(s.PlayerId, req.ReqId, reply);
        s.Conn?.Enqueue(reply);
    }

    void SendError(Session s, Message req, string code, string text) =>
        s.Conn?.Enqueue(Message.Create(MessageTypes.Error, new ErrorPayload(code, text), req.ReqId).ToJson());

    void Remember(string playerId, string reqId, string reply)
    {
        _processed[(playerId, reqId)] = reply;
        if (!_processedOrder.TryGetValue(playerId, out var q))
            _processedOrder[playerId] = q = new Queue<string>();
        q.Enqueue(reqId);
        while (q.Count > Protocol.MaxProcessedPerPlayer)
            _processed.Remove((playerId, q.Dequeue()));
    }
}