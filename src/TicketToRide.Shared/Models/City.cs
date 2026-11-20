namespace TicketToRide.Shared.Models;

public class City
{
    public string CityCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public float X { get; set; }
    public float Y { get; set; }
}