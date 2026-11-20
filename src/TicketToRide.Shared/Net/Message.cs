using System.Text.Json;
using System.Text.Json.Serialization;

namespace TicketToRide.Shared.Net;

public static class Json
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}

/// <summary>
/// Phong bì chung cho mọi thông điệp.
/// TODO: đối chiếu với tài liệu giao thức (dòng 7) để thêm/đổi tên trường cho khớp.
/// </summary>
public sealed class Message
{
    public string Type { get; set; } = "";
    public string? ReqId { get; set; }            // client sinh; server phản hồi lại cùng reqId
    public int Version { get; set; } = Protocol.Version;
    public JsonElement? Payload { get; set; }

    public static Message Create(string type, object? payload = null, string? reqId = null) => new()
    {
        Type = type,
        ReqId = reqId,
        Payload = payload == null ? null : JsonSerializer.SerializeToElement(payload, Json.Options),
    };

    public string ToJson() => JsonSerializer.Serialize(this, Json.Options);

    public static Message? Parse(string json)
    {
        try { return JsonSerializer.Deserialize<Message>(json, Json.Options); }
        catch (JsonException) { return null; }
    }

    public T? ReadPayload<T>()
    {
        if (Payload is not JsonElement el || el.ValueKind != JsonValueKind.Object) return default;
        try { return el.Deserialize<T>(Json.Options); }
        catch (JsonException) { return default; }
    }
}
