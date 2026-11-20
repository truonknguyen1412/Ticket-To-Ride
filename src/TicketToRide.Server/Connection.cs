using System.Net.Sockets;
using System.Threading.Channels;
using TicketToRide.Shared;
using TicketToRide.Shared.Net;

namespace TicketToRide.Server;

/// <summary>
/// Một thể hiện cho mỗi client: đọc khung, hàng đợi ghi (đúng một luồng ghi).
/// Không biết luật chơi. Hàng đợi ghi đầy (256 khung) thì đóng kết nối (mục 5.1).
/// </summary>
public sealed class Connection
{
    readonly TcpClient _tcp;
    readonly NetworkStream _stream;
    readonly Channel<string> _outbox = Channel.CreateBounded<string>(
        new BoundedChannelOptions(Protocol.OutboxCapacity)
        {
            SingleReader = true,
            FullMode = BoundedChannelFullMode.Wait,
        });

    int _closed;
    long _lastSeenTicks = DateTime.UtcNow.Ticks;

    public Connection(TcpClient tcp)
    {
        _tcp = tcp;
        _stream = tcp.GetStream();
        RemoteEndPoint = tcp.Client.RemoteEndPoint?.ToString() ?? "?";
    }

    public Guid Id { get; } = Guid.NewGuid();
    public string RemoteEndPoint { get; }
    public Session? Session { get; set; }
    public DateTime LastSeenUtc => new(Interlocked.Read(ref _lastSeenTicks), DateTimeKind.Utc);
    public bool IsClosed => Volatile.Read(ref _closed) == 1;

    /// <summary>Số lần xác thực thất bại liên tiếp trên kết nối này (chống dò mật khẩu/token).</summary>
    public int AuthFailures { get; set; }

    /// <summary>Đẩy một khung vào hàng đợi ghi. Phòng chỉ gọi hàm này, không ghi thẳng vào socket.</summary>
    public bool Enqueue(string frame)
    {
        if (Volatile.Read(ref _closed) == 1) return false;
        if (_outbox.Writer.TryWrite(frame)) return true;
        Close();     // hàng đợi đầy: đóng và coi như ngắt kết nối
        return false;
    }

    public async Task RunAsync(Func<Connection, string, Task> onFrame, CancellationToken ct)
    {
        var writer = WriteLoopAsync(ct);
        await ReadLoopAsync(onFrame, ct);
        Close();
        await writer;
    }

    async Task ReadLoopAsync(Func<Connection, string, Task> onFrame, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var json = await Framing.ReadAsync(_stream, ct);
                if (json == null) break;
                Interlocked.Exchange(ref _lastSeenTicks, DateTime.UtcNow.Ticks);
                await onFrame(this, json);      // chờ xong mới đọc khung kế tiếp -> giữ đúng thứ tự
            }
        }
        catch (Exception)
        {
            // socket lỗi/đóng, khung sai kích thước...: coi như ngắt kết nối
        }
    }

    async Task WriteLoopAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var frame in _outbox.Reader.ReadAllAsync(ct))
                await Framing.WriteAsync(_stream, frame, ct);
        }
        catch (Exception)
        {
            Close();
        }
    }

    public void Close()
    {
        if (Interlocked.Exchange(ref _closed, 1) == 1) return;
        _outbox.Writer.TryComplete();
        try { _tcp.Close(); } catch { }
    }
}
