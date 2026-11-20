using System.Text.Json.Serialization;

namespace TicketToRide.Shared.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TrainColor
{
    // 8 màu bài tàu & tuyến đường chuẩn
    Red,
    Blue,
    Green,
    Yellow,
    Black,
    White,
    Orange,
    Pink,

    // Màu đặc biệt
    Locomotive, // Thẻ đầu máy đa năng (Wildcard)
    Gray        // Tuyến đường tự do (dùng màu nào cũng chiếm được)
}