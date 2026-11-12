using TicketToRide.Shared.Models;

namespace TicketToRide.Shared.Protocol;

public class GameActionResponse
{
    public Guid ReqId { get; set; }
    public Guid MatchId { get; set; }
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public long CurrentStateVersion { get; set; }
}

public class GameStateUpdate
{
    public Guid MatchId { get; set; }
    public GameState GameState { get; set; } = new();
    public string? LastActionSummary { get; set; }
    public bool IsMatchEnded { get; set; }
}

public class ErrorResponse
{
    public string ErrorCode { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}