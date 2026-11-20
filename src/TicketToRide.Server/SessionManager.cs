using System.Collections.Concurrent;
using System.Security.Cryptography;
using TicketToRide.Shared.Net;

namespace TicketToRide.Server;

/// <summary>
/// Nơi duy nhất cấp / xoay / gia hạn / thu hồi token phiên. Mọi thay đổi đều được ghi ra console (nhãn TOKEN).
/// </summary>
public sealed class SessionManager
{
    const string Tag = "TOKEN";
    public static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(Protocol.TokenLifetimeHours);

    readonly ConcurrentDictionary<string, Session> _byPlayer = new();   // playerId -> Session
    readonly ConcurrentDictionary<string, string> _byToken = new();     // resumeToken -> playerId
    readonly ConcurrentDictionary<string, string> _byAccount = new();   // uid Firebase -> playerId
    readonly ServerLogger _log;

    public SessionManager(ServerLogger log) => _log = log;

    public int Count => _byPlayer.Count;

    public Session Create(string playerName, string? accountId = null, string? email = null)
    {
        var s = new Session(Guid.NewGuid().ToString("N"), playerName, NewToken(), accountId, email)
        {
            ExpiresAtUtc = DateTime.UtcNow + TokenLifetime,
        };
        _byPlayer[s.PlayerId] = s;
        _byToken[s.ResumeToken] = s.PlayerId;
        if (accountId != null) _byAccount[accountId] = s.PlayerId;

        _log.Ok(Tag, $"CAP token {ServerLogger.Mask(s.ResumeToken)} cho \"{playerName}\" (playerId {s.PlayerId[..8]}), hết hạn {ServerLogger.Time(s.ExpiresAtUtc)}. Số phiên đang giữ token: {Count}");
        return s;
    }

    public Session? FindByPlayer(string playerId) =>
        _byPlayer.TryGetValue(playerId, out var s) ? s : null;

    public Session? FindByToken(string token) =>
        _byToken.TryGetValue(token, out var id) ? FindByPlayer(id) : null;

    public Session? FindByAccount(string accountId) =>
        _byAccount.TryGetValue(accountId, out var id) ? FindByPlayer(id) : null;

    public bool IsExpired(Session s) => DateTime.UtcNow > s.ExpiresAtUtc;

    /// <summary>Gia hạn kéo dài thêm TokenLifetime (gọi khi nối lại hoặc khi nhận PING).</summary>
    public void Renew(Session s) => s.ExpiresAtUtc = DateTime.UtcNow + TokenLifetime;

    /// <summary>Đổi token mới, token cũ mất hiệu lực ngay (dùng khi cùng tài khoản đăng nhập lại).</summary>
    public void Rotate(Session s)
    {
        var old = s.ResumeToken;
        _byToken.TryRemove(old, out _);
        s.ResumeToken = NewToken();
        _byToken[s.ResumeToken] = s.PlayerId;
        Renew(s);
        _log.Warn(Tag, $"XOAY token của \"{s.PlayerName}\": {ServerLogger.Mask(old)} (đã vô hiệu) -> {ServerLogger.Mask(s.ResumeToken)}, hết hạn {ServerLogger.Time(s.ExpiresAtUtc)}");
    }

    /// <summary>Gỡ session và thu hồi token.</summary>
    public void Remove(Session s, string reason = "thu hồi")
    {
        if (!_byPlayer.TryRemove(s.PlayerId, out _)) return;       // đã gỡ rồi
        _byToken.TryRemove(s.ResumeToken, out _);
        if (s.AccountId != null)
            _byAccount.TryRemove(new KeyValuePair<string, string>(s.AccountId, s.PlayerId));

        _log.Warn(Tag, $"THU HOI token {ServerLogger.Mask(s.ResumeToken)} của \"{s.PlayerName}\" - lý do: {reason}. Số phiên còn lại: {Count}");
    }

    /// <summary>Dọn các phiên đã mất kết nối, không ở phòng nào và token đã hết hạn.</summary>
    public void PurgeExpired()
    {
        foreach (var s in _byPlayer.Values)
            if (s.Conn == null && s.RoomCode == null && IsExpired(s))
                Remove(s, "token hết hạn");
    }

    static string NewToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
}
