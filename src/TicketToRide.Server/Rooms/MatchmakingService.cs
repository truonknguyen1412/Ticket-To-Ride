using System.Collections.Concurrent;
using TicketToRide.Shared.Net;

namespace TicketToRide.Server;

public sealed class MatchmakingService
{
    sealed class QueueEntry
    {
        public required Session Session { get; init; }
        public required bool IsRanked { get; init; }
        public required int RankPoints { get; init; }
        public required DateTime EnqueuedAtUtc { get; init; }
    }

    readonly object _lock = new();
    readonly RoomManager _rooms;
    readonly ServerLogger _log;
    readonly List<QueueEntry> _rankedQueue = new();
    readonly List<QueueEntry> _casualQueue = new();
    readonly ConcurrentDictionary<string, bool> _inQueue = new(); // playerId -> isRanked
    readonly ConcurrentDictionary<string, DateTime> _bannedUntilUtc = new(); // playerId -> hết hạn cấm tìm trận
    readonly ConcurrentDictionary<string, TimeSpan> _savedElapsed = new(); // playerId -> thời gian đã tìm trận trước lúc vào ReadyCheck

    // Ngưỡng chênh lệch điểm rank: cơ bản +-50, tự nới thêm +25 mỗi 10s chờ (tối đa +-300)
    const int BaseRankSpread = 50;
    const int SpreadStep = 25;
    const int SpreadIntervalSeconds = 10;
    const int MaxRankSpread = 300;

    public MatchmakingService(RoomManager rooms, ServerLogger log)
    {
        _rooms = rooms;
        _log = log;
    }

    public int GetRemainingBanSeconds(string playerId)
    {
        if (!_bannedUntilUtc.TryGetValue(playerId, out var until))
            return 0;

        int rem = (int)Math.Ceiling((until - DateTime.UtcNow).TotalSeconds);
        if (rem <= 0)
        {
            _bannedUntilUtc.TryRemove(playerId, out _);
            return 0;
        }
        return rem;
    }

    public void BanPlayer(string playerId, int seconds = Protocol.MatchPenaltySeconds)
    {
        _savedElapsed.TryRemove(playerId, out _);
        _inQueue.TryRemove(playerId, out _);

        var until = DateTime.UtcNow.AddSeconds(seconds);
        _bannedUntilUtc.AddOrUpdate(playerId, until, (_, old) => old > until ? old : until);

        _log.Warn("MATCH", $"Cấm ghép trận Rank/Thường {seconds}s đối với playerId={playerId[..Math.Min(8, playerId.Length)]}");
    }

    public void ClearSavedElapsed(IEnumerable<string> playerIds)
    {
        foreach (var id in playerIds)
            _savedElapsed.TryRemove(id, out _);
    }

    /// <summary>Đưa người đã sẵn sàng quay lại hàng chờ và tiếp tục đếm từ đúng mốc thời gian tìm trận cũ.</summary>
    public void RequeueAfterReadyCheck(Session session, bool isRanked)
    {
        if (session.Conn == null) return;
        if (GetRemainingBanSeconds(session.PlayerId) > 0) return;

        lock (_lock)
        {
            if (_inQueue.ContainsKey(session.PlayerId)) return;

            TimeSpan priorElapsed = TimeSpan.Zero;
            if (_savedElapsed.TryRemove(session.PlayerId, out var saved) && saved > TimeSpan.Zero)
                priorElapsed = saved;

            var entry = new QueueEntry
            {
                Session = session,
                IsRanked = isRanked,
                RankPoints = isRanked ? session.RankPoints : 0,
                EnqueuedAtUtc = DateTime.UtcNow - priorElapsed
            };

            var queue = isRanked ? _rankedQueue : _casualQueue;
            queue.Add(entry);
            _inQueue[session.PlayerId] = isRanked;

            _log.Info("MATCH", $"\"{session.PlayerName}\" quay lại hàng chờ {(isRanked ? "RANK" : "CASUAL")} tại mốc {(int)priorElapsed.TotalSeconds}s");
            SendStatusLocked(entry, queue.Count);
        }
    }

    public (bool Ok, string ErrorCode, string ErrorText) Enqueue(Session session, bool isRanked)
    {
        int banLeft = GetRemainingBanSeconds(session.PlayerId);
        if (banLeft > 0)
            return (false, ErrorCodes.MatchBanned, $"Bạn chưa sẵn sàng nên bị cấm ghép trận Rank/Thường trong {banLeft} giây.");

        if (_rooms.FindByPlayer(session.PlayerId) != null)
            return (false, ErrorCodes.AlreadyInRoom, "Bạn đang ở trong một phòng");

        if (session.Conn == null)
        {
            _log.Warn("MATCH", $"Chặn \"{session.PlayerName}\" tìm trận: ConnNull=True, State={session.State}");
            return (false, ErrorCodes.BadPayload, "Kết nối chưa sẵn sàng để tìm trận");
        }

        if (session.State == ConnState.LeftMatch)
        {
            session.State = ConnState.Connected;
        }

        if (session.State != ConnState.Connected)
        {
            _log.Warn("MATCH", $"Chặn \"{session.PlayerName}\" tìm trận: ConnNull=False, State={session.State}");
            return (false, ErrorCodes.BadPayload, "Kết nối chưa sẵn sàng để tìm trận");
        }

        lock (_lock)
        {
            if (_inQueue.ContainsKey(session.PlayerId))
                return (false, ErrorCodes.AlreadyInQueue, "Bạn đang ở trong hàng chờ ghép trận");

            DateTime enqueuedAt = DateTime.UtcNow;
            if (_savedElapsed.TryRemove(session.PlayerId, out var priorElapsed) && priorElapsed > TimeSpan.Zero)
                enqueuedAt = enqueuedAt - priorElapsed;

            var entry = new QueueEntry
            {
                Session = session,
                IsRanked = isRanked,
                RankPoints = isRanked ? session.RankPoints : 0,
                EnqueuedAtUtc = enqueuedAt
            };

            var queue = isRanked ? _rankedQueue : _casualQueue;
            queue.Add(entry);
            _inQueue[session.PlayerId] = isRanked;

            _log.Info("MATCH", $"\"{session.PlayerName}\" (rank={session.RankPoints}) vào hàng chờ {(isRanked ? "RANK" : "CASUAL")} ({queue.Count} người)");

            TryMatchLocked(isRanked);

            if (_inQueue.ContainsKey(session.PlayerId))
            {
                SendStatusLocked(entry, queue.Count);
            }
        }

        return (true, "", "");
    }

    public bool Cancel(string playerId)
    {
        lock (_lock)
        {
            _savedElapsed.TryRemove(playerId, out _);
            if (!_inQueue.TryRemove(playerId, out var isRanked))
                return false;

            var queue = isRanked ? _rankedQueue : _casualQueue;
            var entry = queue.FirstOrDefault(e => e.Session.PlayerId == playerId);
            queue.RemoveAll(e => e.Session.PlayerId == playerId);

            var playerName = entry?.Session.PlayerName ?? "Unknown";
            _log.Info("MATCH", $"\"{playerName}\" (id={playerId[..Math.Min(8, playerId.Length)]}) đã rời hàng chờ ghép trận");
            return true;
        }
    }

    public void RemovePlayer(string playerId) => Cancel(playerId);

    /// <summary>Gọi định kỳ mỗi 2-5 giây để tự nới ngưỡng Rank và ghép cặp kể cả khi không có người mới bấm tìm.</summary>
    public void Tick()
    {
        lock (_lock)
        {
            TryMatchLocked(true);
            TryMatchLocked(false);

            foreach (var entry in _rankedQueue)
                SendStatusLocked(entry, _rankedQueue.Count);

            foreach (var entry in _casualQueue)
                SendStatusLocked(entry, _casualQueue.Count);
        }
    }

    void TryMatchLocked(bool isRanked)
    {
        var queue = isRanked ? _rankedQueue : _casualQueue;

        queue.RemoveAll(e =>
        {
            bool invalid = e.Session.Conn == null ||
                           e.Session.State == ConnState.LeftMatch ||
                           GetRemainingBanSeconds(e.Session.PlayerId) > 0 ||
                           _rooms.FindByPlayer(e.Session.PlayerId) != null;
            if (invalid) _inQueue.TryRemove(e.Session.PlayerId, out _);
            return invalid;
        });

        if (queue.Count < Protocol.MinPlayers) return;

        List<QueueEntry>? group = null;

        if (!isRanked)
        {
            // Đấu thường: ghép theo thứ tự vào hàng chờ (tối thiểu MinPlayers, tối đa MaxPlayers)
            int take = Math.Min(Protocol.MaxPlayers, queue.Count);
            group = queue.Take(take).ToList();
        }
        else
        {
            // Xếp hạng: ưu tiên người chờ lâu nhất làm mốc, tìm người có chênh lệch RankPoints trong biên độ cho phép
            var now = DateTime.UtcNow;
            foreach (var anchor in queue.OrderBy(x => x.EnqueuedAtUtc))
            {
                int waitedSec = Math.Max(0, (int)(now - anchor.EnqueuedAtUtc).TotalSeconds);
                int spread = Math.Min(MaxRankSpread, BaseRankSpread + (waitedSec / SpreadIntervalSeconds) * SpreadStep);

                var candidates = queue
                    .Where(x => Math.Abs(x.RankPoints - anchor.RankPoints) <= spread)
                    .OrderBy(x => Math.Abs(x.RankPoints - anchor.RankPoints))
                    .ThenBy(x => x.EnqueuedAtUtc)
                    .Take(Protocol.MaxPlayers)
                    .ToList();

                if (candidates.Count >= Protocol.MinPlayers)
                {
                    group = candidates;
                    break;
                }
            }
        }

        if (group == null) return;

        // Chốt mốc thời gian đã tìm trận của từng người ngay lúc tìm đủ người (không tính thời gian chờ sẵn sàng)
        var foundAtUtc = DateTime.UtcNow;
        foreach (var item in group)
        {
            var elapsed = foundAtUtc - item.EnqueuedAtUtc;
            _savedElapsed[item.Session.PlayerId] = elapsed > TimeSpan.Zero ? elapsed : TimeSpan.Zero;
            queue.Remove(item);
            _inQueue.TryRemove(item.Session.PlayerId, out _);
        }

        var host = group[0].Session;
        var room = _rooms.Create(host, isMatchReadyCheck: true, isRankedMatch: isRanked, matchmaking: this);
        if (room == null)
        {
            foreach (var item in group)
            {
                queue.Insert(0, item);
                _inQueue[item.Session.PlayerId] = isRanked;
            }
            return;
        }

        // Gửi CREATE_ROOM cho host trước để khởi tạo DB/ACK, sau đó mới gửi JOIN_ROOM cho các thành viên còn lại
        var createMsg = Message.Create(MessageTypes.CreateRoom, null, Guid.NewGuid().ToString("N"));
        room.Inbox.Writer.TryWrite(new ClientMessage(host, createMsg));

        foreach (var member in group.Skip(1))
        {
            var joinMsg = Message.Create(
                MessageTypes.JoinRoom,
                new JoinRoomPayload(room.Code),
                Guid.NewGuid().ToString("N"));
            room.Inbox.Writer.TryWrite(new ClientMessage(member.Session, joinMsg));
        }

        var players = group.Select((x, idx) => new PlayerPublic(
            x.Session.PlayerId,
            x.Session.PlayerName,
            WireStatus.Connected,
            false,
            idx == 0)).ToList();

        var matchFoundJson = Message.Create(
            MessageTypes.MatchFound,
            new MatchFoundPayload(room.Code, isRanked, players)).ToJson();

        foreach (var item in group)
        {
            item.Session.Conn?.Enqueue(matchFoundJson);
        }

        _log.Ok("MATCH", $"Ghép xong phòng chờ sẵn sàng {room.Code} ({(isRanked ? "RANK" : "CASUAL")}) với {group.Count} người chơi");
    }

    static void SendStatusLocked(QueueEntry entry, int queuedCount)
    {
        int elapsed = Math.Max(0, (int)(DateTime.UtcNow - entry.EnqueuedAtUtc).TotalSeconds);
        var json = Message.Create(
            MessageTypes.MatchStatus,
            new MatchStatusPayload(entry.IsRanked, queuedCount, Protocol.MinPlayers, elapsed)).ToJson();
        entry.Session.Conn?.Enqueue(json);
    }
}