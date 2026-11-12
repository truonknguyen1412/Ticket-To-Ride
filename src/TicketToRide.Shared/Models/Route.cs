namespace TicketToRide.Shared.Models;

public class Route
{
    public string RouteCode { get; set; } = string.Empty;
    public string CityACode { get; set; } = string.Empty;
    public string CityBCode { get; set; } = string.Empty;
    public byte ParallelIndex { get; set; }
    public byte RouteLength { get; set; }
    public string? ColorCode { get; set; }
    public Guid? OwnerMatchPlayerId { get; set; }

    public bool IsClaimed() => OwnerMatchPlayerId.HasValue;

    public bool IsGrayRoute() => string.IsNullOrEmpty(ColorCode);

    public void SetOwner(Guid playerId) => OwnerMatchPlayerId = playerId;

    public void ClearOwner() => OwnerMatchPlayerId = null;
}