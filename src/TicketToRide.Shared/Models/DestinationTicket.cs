namespace TicketToRide.Shared.Models;

public class DestinationTicket
{
    public string TicketCode { get; set; } = string.Empty;
    public string CityACode { get; set; } = string.Empty;
    public string CityBCode { get; set; } = string.Empty;
    public int Points { get; set; }
}