using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace TicketToRide.Server;

/// <summary>Một thể hiện cho cả server (mục 5).</summary>
public sealed class RoomManager
{
    readonly ConcurrentDictionary<string, Room> _rooms = new();          // roomCode -> Room
    readonly ConcurrentDictionary<string, string> _roomOfPlayer = new(); // playerId -> roomCode
    readonly SessionManager _sessions;

    public RoomManager(SessionManager sessions) => _sessions = sessions;

    /// <summary>Mã phòng 6 chữ số, TryAdd, trùng thì thử lại tối đa 10 lần.</summary>
    public Room? Create(Session host)
    {
        for (int i = 0; i < 10; i++)
        {
            var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
            var room = new Room(code, host, _sessions, this);
            if (_rooms.TryAdd(code, room))
            {
                Bind(host, code);
                _ = room.RunAsync();
                return room;
            }
        }
        return null;
    }

    public Room? Find(string code) => _rooms.TryGetValue(code, out var r) ? r : null;

    public Room? FindByPlayer(string playerId) =>
        _roomOfPlayer.TryGetValue(playerId, out var code) ? Find(code) : null;

    public void Bind(Session s, string code)
    {
        _roomOfPlayer[s.PlayerId] = code;
        s.RoomCode = code;
    }

    public void Unbind(Session s)
    {
        _roomOfPlayer.TryRemove(s.PlayerId, out _);
        s.RoomCode = null;
    }

    public void Remove(string code) => _rooms.TryRemove(code, out _);
}
