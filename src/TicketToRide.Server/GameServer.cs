using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using TicketToRide.Shared;
using TicketToRide.Shared.Net;

namespace TicketToRide.Server;

public sealed class GameServer
{
    readonly SessionManager _sessions = new();
    readonly RoomManager _rooms;
    readonly Dispatcher _dispatcher;
    readonly ConcurrentDictionary<Guid, Connection> _connections = new();
    TcpListener? _listener;
    CancellationTokenSource? _cts;

    public event Action<string>? Log;

    public GameServer()
    {
        _rooms = new RoomManager(_sessions);
        _dispatcher = new Dispatcher(_sessions, _rooms);
    }

    public void Start(int port)
    {
        if (_listener != null) throw new InvalidOperationException("Server đang chạy");

        _cts = new CancellationTokenSource();
        _listener = new TcpListener(IPAddress.Any, port);
        _listener.Start();
        Log?.Invoke($"Server lắng nghe cổng {port}");

        _ = AcceptLoopAsync(_listener, _cts.Token);
        _ = WatchdogLoopAsync(_cts.Token);
    }

    public void Stop()
    {
        _cts?.Cancel();
        _listener?.Stop();
        _listener = null;
        foreach (var c in _connections.Values) c.Close();
        Log?.Invoke("Server đã dừng");
    }

    async Task AcceptLoopAsync(TcpListener listener, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            TcpClient tcp;
            try { tcp = await listener.AcceptTcpClientAsync(ct); }
            catch (OperationCanceledException) { break; }
            catch (ObjectDisposedException) { break; }
            catch (SocketException) { break; }

            _ = HandleClientAsync(tcp, ct);
        }
    }

    async Task HandleClientAsync(TcpClient tcp, CancellationToken ct)
    {
        tcp.NoDelay = true;
        var conn = new Connection(tcp);
        _connections[conn.Id] = conn;
        Log?.Invoke($"+ {conn.RemoteEndPoint}");
        try
        {
            await conn.RunAsync(_dispatcher.Handle, ct);
        }
        finally
        {
            _connections.TryRemove(conn.Id, out _);
            OnClosed(conn);
            Log?.Invoke($"- {conn.RemoteEndPoint}");
        }
    }

    /// <summary>Mất kết nối: đẩy ConnectionLost vào phòng (không xóa người chơi ngay, còn ân hạn).</summary>
    void OnClosed(Connection conn)
    {
        var s = conn.Session;
        if (s == null) return;
        if (!ReferenceEquals(s.Conn, conn)) return;       // đã nối lại bằng kết nối khác

        s.Conn = null;
        var room = _rooms.FindByPlayer(s.PlayerId);
        if (room == null) { _sessions.Remove(s); return; }  // không ở phòng nào thì không có gì để nối lại
        room.Inbox.Writer.TryWrite(new ConnectionLost(s.PlayerId));
    }

    /// <summary>45 giây không có khung hợp lệ thì đóng kết nối (mục 4).</summary>
    async Task WatchdogLoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                var limit = DateTime.UtcNow.AddSeconds(-Protocol.DeadAfterSeconds);
                foreach (var c in _connections.Values)
                {
                    if (c.LastSeenUtc < limit)
                    {
                        Log?.Invoke($"! {c.RemoteEndPoint} quá {Protocol.DeadAfterSeconds}s không hoạt động");
                        c.Close();
                    }
                }
            }
        }
        catch (OperationCanceledException) { }
    }
}
