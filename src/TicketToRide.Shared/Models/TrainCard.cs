namespace TicketToRide.Shared.Models;

public class TrainCard
{
    public Guid CardId { get; set; }
    public string? ColorCode { get; set; }
    public bool IsLocomotive { get; set; }

    public bool IsWildCard() => IsLocomotive;
}