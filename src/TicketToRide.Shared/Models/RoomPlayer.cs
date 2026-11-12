using TicketToRide.Shared.Enums;

namespace TicketToRide.Shared.Models;

public class RoomPlayer
{
    public Guid RoomId { get; set; }
    public Guid AccountId { get; set; }
    public DateTime JoinedAtUtc { get; set; }
    public MemberStatus MemberStatus { get; set; } = MemberStatus.Joined;
}