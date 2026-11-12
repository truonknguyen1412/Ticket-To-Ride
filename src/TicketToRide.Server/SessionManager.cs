using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace TicketToRide.Server;

public sealed class SessionManager
{
    readonly ConcurrentDictionary<string, Session> _byPlayer = new();   // playerId -> Session
    readonly ConcurrentDictionary<string, string> _byToken = new();     // resumeToken -> playerId

    public Session Create(string playerName)
    {
        var s = new Session(Guid.NewGuid().ToString("N"), playerName, NewToken());
        _byPlayer[s.PlayerId] = s;
        _byToken[s.ResumeToken] = s.PlayerId;
        return s;
    }

    public Session? FindByPlayer(string playerId) =>
        _byPlayer.TryGetValue(playerId, out var s) ? s : null;

    public Session? FindByToken(string token) =>
        _byToken.TryGetValue(token, out var id) ? FindByPlayer(id) : null;

    /// <summary>Gỡ session và thu hồi resumeToken.</summary>
    public void Remove(Session s)
    {
        _byPlayer.TryRemove(s.PlayerId, out _);
        _byToken.TryRemove(s.ResumeToken, out _);
    }

    static string NewToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
}
