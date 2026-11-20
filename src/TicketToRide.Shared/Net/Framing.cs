using System.Buffers.Binary;
using System.Text;

namespace TicketToRide.Shared.Net;

/// <summary>
/// Khung thông điệp: 4 byte độ dài (big-endian) + JSON UTF-8. Tối đa 1 048 576 byte.
/// TODO: đối chiếu định dạng khung với tài liệu giao thức mục 2.
/// </summary>
public static class Framing
{
    public static async Task WriteAsync(Stream s, string json, CancellationToken ct = default)
    {
        var body = Encoding.UTF8.GetBytes(json);
        if (body.Length == 0 || body.Length > Protocol.MaxFrameBytes)
            throw new InvalidDataException($"Kích thước khung không hợp lệ: {body.Length}");

        var buf = new byte[4 + body.Length];
        BinaryPrimitives.WriteInt32BigEndian(buf.AsSpan(0, 4), body.Length);
        body.CopyTo(buf, 4);
        await s.WriteAsync(buf, 0, buf.Length, ct);
        await s.FlushAsync(ct);
    }

    /// <returns>null nếu đối phương đã đóng kết nối.</returns>
    public static async Task<string?> ReadAsync(Stream s, CancellationToken ct = default)
    {
        var header = new byte[4];
        if (!await ReadExactAsync(s, header, ct)) return null;

        int len = BinaryPrimitives.ReadInt32BigEndian(header);
        if (len <= 0 || len > Protocol.MaxFrameBytes)
            throw new InvalidDataException($"Độ dài khung không hợp lệ: {len}");

        var body = new byte[len];
        if (!await ReadExactAsync(s, body, ct)) return null;
        return Encoding.UTF8.GetString(body);
    }

    static async Task<bool> ReadExactAsync(Stream s, byte[] buf, CancellationToken ct)
    {
        int off = 0;
        while (off < buf.Length)
        {
            int n = await s.ReadAsync(buf, off, buf.Length - off, ct);
            if (n == 0) return false;
            off += n;
        }
        return true;
    }
}
