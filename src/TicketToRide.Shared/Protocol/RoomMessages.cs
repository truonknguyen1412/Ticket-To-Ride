using TicketToRide.Shared.Models;

namespace TicketToRide.Shared.Protocol;

public enum RoomActionType
{
    GetRooms,
    CreateRoom,
    JoinRoom,
    LeaveRoom,
    StartMatch
}

public class RoomRequest
{
    public RoomActionType Action { get; set; }
    public Guid? RoomId { get; set; }
    public string? RoomName { get; set; }
    public int MaxPlayers { get; set; } = 4;
}

public class RoomResponse
{
    public RoomActionType Action { get; set; }
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public Room? Room { get; set; }
    public List<Room>? Rooms { get; set; }
    public Match? StartedMatch { get; set; }
}