using TicketToRide.Shared.Enums;

namespace TicketToRide.Shared.Models;

public class Match
{
    public Guid MatchId { get; set; }
    public Guid RoomId { get; set; }
    public MatchStatus Status { get; set; } = MatchStatus.InProgress;
    public DateTime StartedAtUtc { get; set; }
    public DateTime? EndedAtUtc { get; set; }
    public GameState GameState { get; set; } = new();

    public void Start()
    {
        Status = MatchStatus.InProgress;
        StartedAtUtc = DateTime.UtcNow;
    }

    public void Complete()
    {
        Status = MatchStatus.Completed;
        EndedAtUtc = DateTime.UtcNow;
    }

    public void Abandon()
    {
        Status = MatchStatus.Abandoned;
        EndedAtUtc = DateTime.UtcNow;
    }

    public void SetRecoveryPending() => Status = MatchStatus.RecoveryPending;
}