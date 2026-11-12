using System.Threading.Channels;
using TicketToRide.Shared;
using TicketToRide.Shared.Net;

namespace TicketToRide.Server;

/// <summary>Trạng thái nội bộ (mục 3). Trên đường truyền chỉ có LOBBY, IN_GAME, ENDED.</summary>
public enum RoomStatus { Lobby, InGame, Ended, Recovering, Closed }

public sealed class Member
{
    public Member(Session session) => Session = session;
    public Session Session { get; }
    public bool Ready { get; set; }
}

/// <summary>
/// Room (actor): sở hữu trạng thái phòng, xử lý lệnh tuần tự trên đúng một vòng lặp,
/// nên không cần khóa. Phòng không ghi thẳng vào socket mà gọi Connection.Enqueue.
/// Đã làm: phòng chờ (tạo/vào/sẵn sàng/bắt đầu/rời), ân hạn 30 giây, cache reqId.
/// Chưa làm (để các dòng sau): GameEngine, SETUP_TICKETS, đồng hồ lượt 60 giây, SQL Server, RECOVERING.
/// </summary>
public sealed class Room
{
    readonly SessionManager _sessions;
    readonly RoomManager _manager;
    readonly List<Member> _members = new();                                // theo thứ tự vào phòng
    readonly Dictionary<(string PlayerId, string ReqId), string> _processed = new();
    readonly Dictionary<string, Queue<string>> _processedOrder = new();
    readonly Dictionary<string, int> _graceSeq = new();                    // playerId -> số thứ tự đồng hồ ân hạn đang chạy
    int _seqCounter;

    public Room(string code, Session host, SessionManager sessions, RoomManager manager)
    {
        Code = code;
        HostId = host.PlayerId;
        _sessions = sessions;
        _manager = manager;
        _members.Add(new Member(host));
    }

    public string Code { get; }
    public string HostId { get; private set; }
    public RoomStatus Status { get; private set; } = RoomStatus.Lobby;

    /// <summary>Đúng một reader.</summary>
    public Channel<RoomCommand> Inbox { get; } =
        Channel.CreateUnbounded<RoomCommand>(new UnboundedChannelOptions { SingleReader = true });

    public Task RunAsync() => Task.Run(async () =>
    {
        await foreach (var cmd in Inbox.Reader.ReadAllAsync())
        {
            try { Handle(cmd); }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[Room {Code}] {ex}");
                if (cmd is ClientMessage c)
                    SendError(c.Session, c.Msg, ErrorCodes.InternalError, "Lỗi nội bộ của server");
            }
        }
    });

    // ------------------------------------------------------------ lệnh

    void Handle(RoomCommand cmd)
    {
        switch (cmd)
        {
            case ClientMessage c: OnClientMessage(c); break;
            case ConnectionLost l: OnConnectionLost(l.PlayerId); break;
            case ConnectionRestored r: OnConnectionRestored(r.PlayerId); break;
            case TimerFired t: OnTimerFired(t); break;
            case RecoveryConfirm: break; // TODO
        }
    }

    void OnClientMessage(ClientMessage c)
    {
        var s = c.Session;
        var msg = c.Msg;

        // Trùng reqId: trả lại phản hồi cũ, không xử lý lại (mục 5.2, bước 1)
        if (msg.ReqId != null && _processed.TryGetValue((s.PlayerId, msg.ReqId), out var cached))
        {
            s.Conn?.Enqueue(cached);
            return;
        }

        switch (msg.Type)
        {
            case MessageTypes.CreateRoom: AckAndBroadcast(s, msg, new AckPayload(Code)); break;
            case MessageTypes.JoinRoom: OnJoin(s, msg); break;
            case MessageTypes.SetReady: OnSetReady(s, msg); break;
            case MessageTypes.StartGame: OnStartGame(s, msg); break;
            case MessageTypes.LeaveRoom: OnLeave(s, msg); break;
            default: SendError(s, msg, ErrorCodes.UnknownType, $"Phòng không xử lý thông điệp {msg.Type}"); break;
        }
    }

    void OnJoin(Session s, Message msg)
    {
        if (Status != RoomStatus.Lobby) { SendError(s, msg, ErrorCodes.RoomInGame, "Phòng đã bắt đầu ván"); return; }
        if (_members.Count >= Protocol.MaxPlayers) { SendError(s, msg, ErrorCodes.RoomFull, "Phòng đã đầy"); return; }
        if (_members.Any(m => string.Equals(m.Session.PlayerName, s.PlayerName, StringComparison.OrdinalIgnoreCase)))
        { SendError(s, msg, ErrorCodes.NameTaken, "Tên người chơi đã có trong phòng"); return; }

        _members.Add(new Member(s));
        _manager.Bind(s, Code);
        AckAndBroadcast(s, msg, new AckPayload(Code));
    }

    void OnSetReady(Session s, Message msg)
    {
        var m = Find(s.PlayerId);
        if (m == null) { SendError(s, msg, ErrorCodes.NotInRoom, "Bạn không ở trong phòng"); return; }
        if (Status != RoomStatus.Lobby) { SendError(s, msg, ErrorCodes.RoomInGame, "Phòng đã bắt đầu ván"); return; }

        var p = msg.ReadPayload<SetReadyPayload>();
        if (p == null) { SendError(s, msg, ErrorCodes.BadPayload, "Thiếu trường ready"); return; }

        m.Ready = p.Ready;
        AckAndBroadcast(s, msg, null);
    }

    void OnStartGame(Session s, Message msg)
    {
        if (Find(s.PlayerId) == null) { SendError(s, msg, ErrorCodes.NotInRoom, "Bạn không ở trong phòng"); return; }
        if (Status != RoomStatus.Lobby) { SendError(s, msg, ErrorCodes.RoomInGame, "Phòng đã bắt đầu ván"); return; }
        if (s.PlayerId != HostId) { SendError(s, msg, ErrorCodes.NotHost, "Chỉ chủ phòng được bắt đầu"); return; }
        if (_members.Count < Protocol.MinPlayers || _members.Count > Protocol.MaxPlayers)
        { SendError(s, msg, ErrorCodes.NotEnoughPlayers, "Cần 2–4 người chơi"); return; }
        if (_members.Any(m => m.Session.PlayerId != HostId && (!m.Ready || m.Session.State != ConnState.Connected)))
        { SendError(s, msg, ErrorCodes.NotAllReady, "Chưa phải ai cũng sẵn sàng"); return; }

        Status = RoomStatus.InGame;
        // TODO (Quang Trường): khởi tạo GameState, vào SETUP_TICKETS; ghi SQL Server từ đây
        AckAndBroadcast(s, msg, null);
    }

    void OnLeave(Session s, Message msg)
    {
        var m = Find(s.PlayerId);
        if (m == null) { SendError(s, msg, ErrorCodes.NotInRoom, "Bạn không ở trong phòng"); return; }
        if (Status is RoomStatus.InGame or RoomStatus.Recovering)
        { SendError(s, msg, ErrorCodes.RoomInGame, "Đang trong ván, muốn thoát thì đóng kết nối"); return; }

        RemoveMember(m);
        SendAck(s, msg, null);       // s đã rời phòng nên chỉ nhận ACK, không nhận ROOM_UPDATED
        AfterMemberRemoved();
    }

    // ------------------------------------------------------------ kết nối

    void OnConnectionLost(string playerId)
    {
        var m = Find(playerId);
        if (m == null) return;
        var s = m.Session;
        if (s.Conn != null) return;                       // đã nối lại bằng kết nối khác, sự kiện cũ
        if (s.State == ConnState.LeftMatch) return;

        s.State = ConnState.Disconnected;

        if (Status == RoomStatus.Lobby)
        {
            var seq = ++_seqCounter;
            _graceSeq[playerId] = seq;
            StartTimer(seq, playerId, Protocol.LobbyGraceSeconds);
        }
        // TODO (dòng 23): trong ván — đến lượt thì chờ 60 giây rồi LEFT_MATCH; token hết hạn sau 10 phút

        BroadcastConnection(s);
    }

    void OnConnectionRestored(string playerId)
    {
        var m = Find(playerId);
        if (m == null) return;
        var s = m.Session;

        _graceSeq.Remove(playerId);                       // hủy đồng hồ ân hạn
        s.State = ConnState.Connected;

        // Phòng chờ: ROOM_UPDATED. TODO: trong ván gửi GAME_SNAPSHOT (kèm pendingOffer nếu đang chọn vé)
        s.Conn?.Enqueue(Message.Create(MessageTypes.RoomUpdated, BuildUpdate()).ToJson());
        BroadcastConnection(s);
    }

    void OnTimerFired(TimerFired t)
    {
        if (!_graceSeq.TryGetValue(t.PlayerId, out var current) || current != t.Seq) return; // đồng hồ đã hủy
        var m = Find(t.PlayerId);
        if (m == null || m.Session.State != ConnState.Disconnected) return;

        _graceSeq.Remove(t.PlayerId);
        var s = m.Session;
        RemoveMember(m);
        _sessions.Remove(s);                              // hết ân hạn: thu hồi resumeToken
        AfterMemberRemoved();
    }

    /// <summary>Hết giờ, đồng hồ chỉ đẩy một lệnh TimerFired vào hàng đợi, không tự đổi trạng thái.</summary>
    void StartTimer(int seq, string playerId, int seconds)
    {
        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(seconds));
            Inbox.Writer.TryWrite(new TimerFired(seq, playerId));
        });
    }

    // ------------------------------------------------------------ thành viên / đóng phòng

    Member? Find(string playerId) => _members.FirstOrDefault(m => m.Session.PlayerId == playerId);

    void RemoveMember(Member m)
    {
        _members.Remove(m);
        _graceSeq.Remove(m.Session.PlayerId);
        _manager.Unbind(m.Session);
    }

    void AfterMemberRemoved()
    {
        if (_members.Count == 0) { Close(); return; }   // phòng chờ trống: CLOSED ngay

        if (_members.All(m => m.Session.PlayerId != HostId))
        {
            // Chuyển chủ cho người vào sớm nhất đang CONNECTED; không có ai thì người vào sớm nhất còn lại
            var next = _members.FirstOrDefault(m => m.Session.State == ConnState.Connected) ?? _members[0];
            HostId = next.Session.PlayerId;
        }
        Broadcast(Message.Create(MessageTypes.RoomUpdated, BuildUpdate()));
    }

    void Close()
    {
        if (Status == RoomStatus.Closed) return;
        Status = RoomStatus.Closed;

        foreach (var m in _members.ToList())
        {
            _manager.Unbind(m.Session);
            if (m.Session.Conn == null) _sessions.Remove(m.Session);
        }
        _members.Clear();
        _graceSeq.Clear();
        _manager.Remove(Code);
        Inbox.Writer.TryComplete();                      // vòng lặp kết thúc sau khi xử lý nốt lệnh còn lại
    }

    // ------------------------------------------------------------ gửi

    RoomUpdatedPayload BuildUpdate() => new(
        Code,
        Status switch
        {
            RoomStatus.Lobby => WireStatus.Lobby,
            RoomStatus.Ended or RoomStatus.Closed => WireStatus.Ended,
            _ => WireStatus.InGame,                      // IN_GAME và RECOVERING
        },
        HostId,
        _members.Select(m => new PlayerPublic(
            m.Session.PlayerId,
            m.Session.PlayerName,
            ConnStatus(m.Session.State),
            m.Ready,
            m.Session.PlayerId == HostId)).ToList());

    static string ConnStatus(ConnState st) => st switch
    {
        ConnState.Connected => WireStatus.Connected,
        ConnState.LeftMatch => WireStatus.LeftMatch,
        _ => WireStatus.Disconnected,                    // DISCONNECTED và AWAITING_CONFIRM
    };

    void Broadcast(Message m)
    {
        var json = m.ToJson();
        foreach (var mem in _members) mem.Session.Conn?.Enqueue(json);
    }

    void BroadcastConnection(Session s) =>
        Broadcast(Message.Create(MessageTypes.PlayerConnection,
            new PlayerConnectionPayload(s.PlayerId, ConnStatus(s.State))));

    void AckAndBroadcast(Session s, Message req, object? ackPayload)
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
        while (q.Count > Protocol.MaxProcessedPerPlayer)      // tối đa 200 mỗi người
            _processed.Remove((playerId, q.Dequeue()));
    }
}
