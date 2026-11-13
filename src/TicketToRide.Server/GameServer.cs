using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using TicketToRide.Shared;
using TicketToRide.Shared.Net;

namespace TicketToRide.Server;

public sealed class GameServer
{
    readonly ServerOptions _options;
    readonly ServerLogger _log;
    readonly SessionManager _sessions;
    readonly RoomManager _rooms;
    readonly AccountStore _accounts;
    readonly Dispatcher _dispatcher;
    readonly ConcurrentDictionary<Guid, Connection> _connections = new();
    TcpListener? _listener;
    CancellationTokenSource? _cts;

    public GameServer(ServerOptions options, ServerLogger log)
    {
        _options = options;
        _log = log;
        _sessions = new SessionManager(log);
        _rooms = new RoomManager(_sessions);
        _accounts = new AccountStore(options.AccountsFile);

        IIdentityProvider? idp = options.FirebaseApiKey != null ? new FirebaseIdentityProvider(options.FirebaseApiKey) : null;
        var auth = new AuthService(_sessions, _rooms, _accounts, idp, log);
        _dispatcher = new Dispatcher(_sessions, _rooms, auth, options, log);
    }

    public void Start()
    {
        if (_listener != null) throw new InvalidOperationException("Server đang chạy");

        _cts = new CancellationTokenSource();
        _listener = new TcpListener(IPAddress.Any, _options.Port);
        _listener.Start();

        _log.Ok("HE THONG", $"Server lắng nghe cổng {_options.Port}");
        _log.Info("HE THONG", $"Tài khoản đã biết: {_accounts.Count} (file {_accounts.FilePath})");
        _log.Info("HE THONG", $"Token phiên: sống {Protocol.TokenLifetimeHours} giờ, gia hạn khi nối lại hoặc nhận PING; sai xác thực {Protocol.MaxAuthFailures} lần thì đóng kết nối");
        if (_options.FirebaseApiKey != null)
            _log.Ok("HE THONG", $"Xác thực: Firebase (API key {ServerLogger.Mask(_options.FirebaseApiKey)})");
        else
            _log.Warn("HE THONG", "Chưa có FIREBASE_API_KEY -> đăng ký/đăng nhập/quên mật khẩu sẽ bị từ chối (AUTH_UNAVAILABLE)");
        if (_options.AllowAnonymous)
            _log.Warn("HE THONG", "Chế độ --dev: cho phép HELLO chỉ bằng tên, KHÔNG xác thực. Đừng dùng khi chạy thật.");

        _ = AcceptLoopAsync(_listener, _cts.Token);
        _ = WatchdogLoopAsync(_cts.Token);
    }

    public void Stop()
    {
        _cts?.Cancel();
        _listener?.Stop();
        _listener = null;
        foreach (var c in _connections.Values) c.Close();
        _log.Info("HE THONG", "Server đã dừng");
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
        _log.Info("KET NOI", $"+ {conn.RemoteEndPoint} (đang mở: {_connections.Count})");
        try
        {
            await conn.RunAsync(_dispatcher.Handle, ct);
        }
        finally
        {
            _connections.TryRemove(conn.Id, out _);
            OnClosed(conn);
            _log.Info("KET NOI", $"- {conn.RemoteEndPoint} (đang mở: {_connections.Count})");
        }
    }

    /// <summary>Một kết nối đóng: đẩy ConnectionLost vào phòng (không xóa người chơi ngay, còn ân hạn).</summary>
    void OnClosed(Connection conn)
    {
        var s = conn.Session;
        if (s == null) return;                              // chưa đăng nhập hoặc đã đăng xuất
        if (!ReferenceEquals(s.Conn, conn)) return;         // đã nối lại bằng kết nối khác

        s.Conn = null;
        var room = _rooms.FindByPlayer(s.PlayerId);
        if (room == null)
        {
            // Không ở phòng nào: không có gì để nối lại -> thu hồi token luôn
            _log.Info("PHIEN", $"\"{s.PlayerName}\" ngắt kết nối khi không ở phòng (không đăng xuất)");
            _sessions.Remove(s, "mất kết nối khi không ở phòng");
            return;
        }

        _log.Warn("PHIEN", $"\"{s.PlayerName}\" mất kết nối khi ở phòng {room.Code}: giữ token {ServerLogger.Mask(s.ResumeToken)} để nối lại");
        room.Inbox.Writer.TryWrite(new ConnectionLost(s.PlayerId));
    }

    /// <summary>45 giây không có khung hợp lệ thì đóng kết nối (mục 4). Cũng dọn token hết hạn.</summary>
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
                        _log.Warn("KET NOI", $"{c.RemoteEndPoint} quá {Protocol.DeadAfterSeconds}s không hoạt động -> đóng");
                        c.Close();
                    }
                }
                _sessions.PurgeExpired();
            }
        }
        catch (OperationCanceledException) { }
    }
}
