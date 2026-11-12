using System.Text.Json;
using TicketToRide.Shared.Enums;

namespace TicketToRide.Shared.Protocol;

public class GameAction
{
    public Guid ReqId { get; set; }
    public Guid MatchId { get; set; }
    public GameActionType ActionType { get; set; }
    public JsonElement Payload { get; set; }
}