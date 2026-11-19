using System.Collections.Concurrent;
using System.Net.Sockets;
using TicketToRide.Shared;
using TicketToRide.Shared.Net;

namespace TicketToRide.ClientCore;

public sealed class ServerErrorException : Exception
{
    public ServerErrorException(string code, string message) : base(message) => Code = code;
    public string Code { get; }
}

/// <summary>
/// Client networking service (không chứa code giao diện).
/// Đóng/đọc khung, sinh reqId, gửi PING mỗi 15 giây, nối lại bằng resumeToken.
/// Sự kiện được đưa về SynchronizationContext của nơi tạo GameClient (UI thread của WinForms),
/// nên hãy tạo GameClient trên UI thread (ví dụ trong constructor của Form).
/// Chưa làm: Kho trạng thái (STATE_UPDATE/GAME_SNAPSHOT), tự gửi SYNC_REQUEST khi lệch phiên bản.
/// </summary>
public sealed class GameClient : IDisposable
{
    readonly SynchronizationContext? _ui = SynchronizationContext.Current;
    readonly ConcurrentDictionary<string, TaskCompletionSource<Message>> _pending = new();
    readonly SemaphoreSlim _writeLock = new(1, 1);

    TcpClient? _tcp;
    NetworkStream? _stream;
    CancellationTokenSource? _cts;
    string? _host;
    int _port;
    string? _playerName;

    public string? PlayerId { get; private set; }
    public string? PlayerName { get; private set; }
    public DateTime? TokenExpiresUtc { get; private set; }
    public string? ResumeToken { get; private set; }

    /// <summary>Thông điệp server chủ động gửi (ROOM_UPDATED, PLAYER_CONNECTION, ...). Chạy trên UI thread nếu có.</summary>
    public event Action<Message>? MessageReceived;

    /// <summary>Mất kết nối ngoài ý muốn (không bắn khi tự gọi DisconnectAsync/Dispose).</summary>
    public event Action<string>? Disconnected;

    // ------------------------------------------------------------ kết nối

    public async Task ConnectAsync(string host, int port, string playerName)
    {
        _host = host;
        _port = port;
        _playerName = playerName;

        await OpenAsync();
        var reply = await RequestAsync(MessageTypes.Hello, new HelloPayload(playerName, null, null));
        var w = reply.ReadPayload<WelcomePayload>()
                ?? throw new InvalidDataException("Phản hồi HELLO không hợp lệ");
        PlayerId = w.PlayerId;
        ResumeToken = w.ResumeToken;
        PlayerName = playerName;
    }

    // ------------------------------------------------------------ xác thực (Firebase idToken -> token phiên của server)

    /// <summary>Mở kết nối TCP nhưng chưa đăng nhập. Sau đó gọi LoginAsync / RegisterAsync / ForgotPasswordAsync.</summary>
    public async Task ConnectAnonymousAsync(string host, int port)
    {
        _host = host;
        _port = port;
        await OpenAsync();
    }

    /// <summary>Đăng nhập bằng idToken của Firebase. Server xác minh rồi cấp token phiên (ResumeToken).</summary>
    public async Task<WelcomePayload> LoginAsync(string firebaseIdToken) =>
        ApplyWelcome(await RequestAsync(MessageTypes.AuthLogin, new AuthLoginPayload(firebaseIdToken), 20_000));

    /// <summary>Đăng ký hồ sơ trên server (sau khi đã tạo tài khoản Firebase) và đăng nhập luôn.</summary>
    public async Task<WelcomePayload> RegisterAsync(string firebaseIdToken, string username) =>
        ApplyWelcome(await RequestAsync(MessageTypes.AuthRegister, new AuthRegisterPayload(firebaseIdToken, username), 20_000));

    /// <summary>Nhờ server gửi email đặt lại mật khẩu. Không cần đăng nhập. Trả về thông báo của server.</summary>
    public async Task<string> ForgotPasswordAsync(string email)
    {
        var r = await RequestAsync(MessageTypes.AuthForgot, new AuthForgotPayload(email), 20_000);
        return r.ReadPayload<AuthAckPayload>()?.Message ?? "Đã gửi email đặt lại mật khẩu.";
    }

    /// <summary>Vào lại bằng token phiên đã lưu (không cần Firebase). Bị từ chối (RESUME_REJECTED / TOKEN_EXPIRED) nếu token đã bị thu hồi hoặc hết hạn.</summary>
    public async Task ResumeAsync(string host, int port, string resumeToken)
    {
        _host = host;
        _port = port;
        await OpenAsync();
        ApplyWelcome(await RequestAsync(MessageTypes.Hello, new HelloPayload(_playerName, resumeToken, null)));
    }

    /// <summary>Đăng xuất: server thu hồi token phiên, sau đó đóng kết nối.</summary>
    public async Task LogoutAsync()
    {
        try { await RequestAsync(MessageTypes.AuthLogout); }
        finally
        {
            PlayerId = null;
            PlayerName = null;
            ResumeToken = null;
            TokenExpiresUtc = null;
            Close();
        }
    }

    WelcomePayload ApplyWelcome(Message reply)
    {
        var w = reply.ReadPayload<WelcomePayload>()
                ?? throw new InvalidDataException("Phản hồi đăng nhập không hợp lệ");
        PlayerId = w.PlayerId;
        ResumeToken = w.ResumeToken;
        PlayerName = w.PlayerName ?? _playerName;
        _playerName = PlayerName;
        TokenExpiresUtc = w.ExpiresInSeconds is int s ? DateTime.UtcNow.AddSeconds(s) : null;
        return w;
    }

    /// <summary>Nối lại bằng resumeToken sau khi mất kết nối. Server sẽ gửi lại ROOM_UPDATED (hoặc GAME_SNAPSHOT).</summary>
    public async Task ReconnectAsync()
    {
        if (_host == null || ResumeToken == null)
            throw new InvalidOperationException("Chưa từng kết nối thành công");

        await OpenAsync();
        await RequestAsync(MessageTypes.Hello, new HelloPayload(_playerName, ResumeToken, null));
    }

    public async Task DisconnectAsync()
    {
        try { await SendAsync(MessageTypes.Bye); } catch { }
        Close();
    }

    public void Dispose() => Close();

    async Task OpenAsync()
    {
        Close();
        _tcp = new TcpClient { NoDelay = true };
        await _tcp.ConnectAsync(_host!, _port);
        _stream = _tcp.GetStream();
        _cts = new CancellationTokenSource();

        _ = ReceiveLoopAsync(_stream, _cts.Token);
        _ = HeartbeatLoopAsync(_cts.Token);
    }

    void Close()
    {
        _cts?.Cancel();
        try { _tcp?.Close(); } catch { }
        _tcp = null;
        _stream = null;
    }

    // ------------------------------------------------------------ gửi / yêu cầu

    /// <summary>Gửi một thông điệp không chờ phản hồi.</summary>
    public Task SendAsync(string type, object? payload = null) =>
        WriteAsync(Message.Create(type, payload));

    /// <summary>Gửi yêu cầu kèm reqId mới, chờ ACK/WELCOME/PONG cùng reqId. Nhận ERROR thì ném ServerErrorException.</summary>
    public async Task<Message> RequestAsync(string type, object? payload = null, int timeoutMs = 10_000)
    {
        var reqId = Guid.NewGuid().ToString("N");
        var tcs = new TaskCompletionSource<Message>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[reqId] = tcs;
        try
        {
            await WriteAsync(Message.Create(type, payload, reqId));

            var done = await Task.WhenAny(tcs.Task, Task.Delay(timeoutMs));
            if (done != tcs.Task) throw new TimeoutException($"Server không phản hồi {type}");

            var reply = await tcs.Task;
            if (reply.Type == MessageTypes.Error)
            {
                var e = reply.ReadPayload<ErrorPayload>();
                throw new ServerErrorException(e?.Code ?? "UNKNOWN", e?.Message ?? "Lỗi không rõ");
            }
            return reply;
        }
        finally
        {
            _pending.TryRemove(reqId, out _);
        }
    }

    async Task WriteAsync(Message m)
    {
        var stream = _stream ?? throw new InvalidOperationException("Chưa kết nối");
        await _writeLock.WaitAsync();                // chỉ một luồng ghi tại một thời điểm
        try { await Framing.WriteAsync(stream, m.ToJson()); }
        finally { _writeLock.Release(); }
    }

    // ------------------------------------------------------------ API phòng chờ

    /// <returns>Mã phòng 6 chữ số.</returns>
    public async Task<string> CreateRoomAsync()
    {
        var r = await RequestAsync(MessageTypes.CreateRoom);
        return r.ReadPayload<AckPayload>()?.RoomCode
               ?? throw new InvalidDataException("ACK không có roomCode");
    }

    public Task JoinRoomAsync(string roomCode) =>
        RequestAsync(MessageTypes.JoinRoom, new JoinRoomPayload(roomCode));

    public Task SetReadyAsync(bool ready) =>
        RequestAsync(MessageTypes.SetReady, new SetReadyPayload(ready));

    public Task StartGameAsync() => RequestAsync(MessageTypes.StartGame);

    public Task LeaveRoomAsync() => RequestAsync(MessageTypes.LeaveRoom);

    /// <summary>Gửi khi phát hiện lệch phiên bản. TODO: tên trường theo tài liệu giao thức.</summary>
    public Task RequestSyncAsync(int stateVersion) =>
        SendAsync(MessageTypes.SyncRequest, new { stateVersion });

    // ------------------------------------------------------------ vòng lặp nền

    async Task ReceiveLoopAsync(NetworkStream stream, CancellationToken ct)
    {
        string reason = "Server đã đóng kết nối";
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var json = await Framing.ReadAsync(stream, ct);
                if (json == null) break;

                var msg = Message.Parse(json);
                if (msg == null) continue;

                if (msg.ReqId != null && _pending.TryRemove(msg.ReqId, out var tcs))
                {
                    tcs.TrySetResult(msg);           // phản hồi cho một yêu cầu đang chờ
                    continue;
                }
                Raise(() => MessageReceived?.Invoke(msg));
            }
        }
        catch (OperationCanceledException) { return; }
        catch (Exception ex) { reason = ex.Message; }

        if (!ReferenceEquals(stream, _stream)) return;      // đã đóng chủ động hoặc đã mở kết nối mới

        foreach (var p in _pending.Values) p.TrySetException(new IOException("Mất kết nối"));
        Raise(() => Disconnected?.Invoke(reason));
    }

    async Task HeartbeatLoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Protocol.HeartbeatSeconds));
        try
        {
            while (await timer.WaitForNextTickAsync(ct))
                await RequestAsync(MessageTypes.Ping);
        }
        catch (OperationCanceledException) { }
        catch (TimeoutException) { try { _tcp?.Close(); } catch { } }   // vòng nhận sẽ báo Disconnected
        catch (Exception) { }
    }

    void Raise(Action a)
    {
        if (_ui != null) _ui.Post(_ => a(), null);
        else a();
    }
}
