using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace TicketToRide.Shared.Protocol;

public enum PacketType
{
    AuthRequest,
    AuthResponse,
    LogoutRequest,
    LogoutResponse,
    RoomRequest,
    RoomResponse,
    GameAction,
    GameStateUpdate,
    GameActionResponse,
    Error
}

public class PacketEnvelope
{
    public PacketType Type { get; set; }
    public string PayloadJson { get; set; } = string.Empty;
}

public static class PacketFraming
{
    private const int MaxPacketSize = 1024 * 1024; // Giới hạn 1MB mỗi gói tin

    public static async Task SendAsync<T>(
        NetworkStream stream,
        PacketType type,
        T payload,
        CancellationToken ct = default)
    {
        var envelope = new PacketEnvelope
        {
            Type = type,
            PayloadJson = JsonSerializer.Serialize(payload)
        };

        byte[] bodyBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(envelope));
        byte[] lengthPrefix = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(lengthPrefix, bodyBytes.Length);

        await stream.WriteAsync(lengthPrefix, ct);
        await stream.WriteAsync(bodyBytes, ct);
        await stream.FlushAsync(ct);
    }

    public static async Task<PacketEnvelope?> ReceiveAsync(
        NetworkStream stream,
        CancellationToken ct = default)
    {
        byte[] lengthBuffer = new byte[4];
        int headerRead = await ReadExactAsync(stream, lengthBuffer, ct);
        if (headerRead == 0) return null; // Kết nối đã đóng sạch

        int bodyLength = BinaryPrimitives.ReadInt32BigEndian(lengthBuffer);
        if (bodyLength <= 0 || bodyLength > MaxPacketSize)
            throw new InvalidDataException($"Kích thước gói tin không hợp lệ: {bodyLength} bytes.");

        byte[] bodyBuffer = new byte[bodyLength];
        int bodyRead = await ReadExactAsync(stream, bodyBuffer, ct);
        if (bodyRead < bodyLength) return null;

        string json = Encoding.UTF8.GetString(bodyBuffer);
        return JsonSerializer.Deserialize<PacketEnvelope>(json);
    }

    private static async Task<int> ReadExactAsync(
        NetworkStream stream,
        byte[] buffer,
        CancellationToken ct)
    {
        int totalRead = 0;
        while (totalRead < buffer.Length)
        {
            int bytesRead = await stream.ReadAsync(
                buffer.AsMemory(totalRead, buffer.Length - totalRead), ct);
            if (bytesRead == 0) return totalRead;
            totalRead += bytesRead;
        }
        return totalRead;
    }
}