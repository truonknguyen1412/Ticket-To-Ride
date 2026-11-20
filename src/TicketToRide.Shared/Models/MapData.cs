using System.Text.Json;
using TicketToRide.Shared.Net;

namespace TicketToRide.Shared.Models;

public class MapData
{
    public string MapVersion { get; set; } = "1.0";
    public List<City> Cities { get; set; } = new();
    public List<Route> Routes { get; set; } = new();
    public List<DestinationTicket> DestinationTickets { get; set; } = new();

    public static MapData Load(string? filePath = null)
    {
        var path = filePath ?? Path.Combine(AppContext.BaseDirectory, "map.json");
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<MapData>(json, Json.Options)
               ?? throw new InvalidDataException("Không thể đọc dữ liệu từ map.json");
    }
}