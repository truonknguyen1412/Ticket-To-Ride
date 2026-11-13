using System.Collections.Concurrent;
using System.Net.Mail;
using TicketToRide.Shared;
using TicketToRide.Shared.Net;

namespace TicketToRide.Server;

/// <summary>
/// Bốn luồng xác thực: đăng ký, đăng nhập, quên mật khẩu, đăng xuất.
/// Mật khẩu chỉ đi giữa client và Firebase. Server nhận idToken của Firebase, nhờ Firebase xác minh,
/// rồi cấp token phiên của riêng server (resumeToken) và quản lý vòng đời token đó trong <see cref="SessionManager"/>.
/// Mỗi bước đều ghi ra console của máy chạy server.
/// </summary>
public sealed class AuthService
{
    const string TagReg = "DANG KY";
    const string TagLogin = "DANG NHAP";
    const string TagForgot = "QUEN MK";
    const string TagLogout = "DANG XUAT";

    readonly SessionManager _sessions;
    readonly RoomManager _rooms;
    readonly AccountStore _accounts;
    readonly IIdentityProvider? _idp;
    readonly ServerLogger _log;
    readonly ConcurrentDictionary<string, DateTime> _forgotAt = new();   // email -> lần xin gần nhất

    public AuthService(SessionManager sessions, RoomManager rooms, AccountStore accounts, IIdentityProvider? idp, ServerLogger log)
    {
        _sessions = sessions;
        _rooms = rooms;
        _accounts = accounts;
        _idp = idp;
        _log = log;
    }

    // ------------------------------------------------------------ đăng ký

    public async Task OnRegisterAsync(Connection conn, Message msg)
    {
        _log.Info(TagReg, $"{conn.RemoteEndPoint} gửi yêu cầu đăng ký");
        var p = msg.ReadPayload<AuthRegisterPayload>();

        var username = p?.Username?.Trim();
        if (string.IsNullOrEmpty(username) || username.Length > Protocol.MaxPlayerNameLength)
        {
            Fail(conn, msg, TagReg, ErrorCodes.BadPayload, $"Tên hiển thị dài 1–{Protocol.MaxPlayerNameLength} ký tự");
            return;
        }

        var user = await VerifyAsync(conn, msg, TagReg, p?.IdToken);
        if (user == null) return;

        if (!_accounts.TryCreate(user.Uid, user.Email, username, out var account))
        {
            Fail(conn, msg, TagReg, ErrorCodes.AccountExists, "Tài khoản đã được đăng ký trên server, hãy đăng nhập");
            return;
        }
        _log.Step(TagReg, $"Đã lưu hồ sơ: \"{account.Username}\" <{account.Email}> uid={user.Uid}. Tổng tài khoản: {_accounts.Count}");

        var session = Establish(conn, account);
        conn.AuthFailures = 0;
        SendWelcome(conn, msg, session);
        _log.Ok(TagReg, $"Đăng ký thành công: \"{account.Username}\" <{account.Email}>, tự đăng nhập, token {ServerLogger.Mask(session.ResumeToken)}");
    }

    // ------------------------------------------------------------ đăng nhập

    public async Task OnLoginAsync(Connection conn, Message msg)
    {
        _log.Info(TagLogin, $"{conn.RemoteEndPoint} gửi yêu cầu đăng nhập");
        var p = msg.ReadPayload<AuthLoginPayload>();

        var user = await VerifyAsync(conn, msg, TagLogin, p?.IdToken);
        if (user == null) return;

        var account = _accounts.RecordLogin(user.Uid, user.Email, PickName(user), out var created);
        if (created)
            _log.Warn(TagLogin, $"uid={user.Uid} chưa có hồ sơ trên server (tạo trực tiếp ở Firebase) -> tự tạo hồ sơ \"{account.Username}\"");

        var session = Establish(conn, account);
        conn.AuthFailures = 0;
        SendWelcome(conn, msg, session);
        _log.Ok(TagLogin, $"Đăng nhập thành công: \"{account.Username}\" <{account.Email}>, lần thứ {account.LoginCount}, token {ServerLogger.Mask(session.ResumeToken)}");
    }

    // ------------------------------------------------------------ quên mật khẩu

    public async Task OnForgotAsync(Connection conn, Message msg)
    {
        _log.Info(TagForgot, $"{conn.RemoteEndPoint} gửi yêu cầu đặt lại mật khẩu");
        var email = msg.ReadPayload<AuthForgotPayload>()?.Email?.Trim();

        if (!IsValidEmail(email))
        {
            Fail(conn, msg, TagForgot, ErrorCodes.EmailInvalid, "Định dạng email không hợp lệ");
            return;
        }

        var addr = email!;
        var key = addr.ToLowerInvariant();
        var now = DateTime.UtcNow;
        if (_forgotAt.TryGetValue(key, out var last) && (now - last).TotalSeconds < Protocol.ForgotCooldownSeconds)
        {
            var wait = (int)Math.Ceiling(Protocol.ForgotCooldownSeconds - (now - last).TotalSeconds);
            Fail(conn, msg, TagForgot, ErrorCodes.Cooldown, $"Vừa gửi email cho địa chỉ này, thử lại sau {wait} giây", countFailure: false);
            return;
        }
        _forgotAt[key] = now;

        if (_idp == null)
        {
            Fail(conn, msg, TagForgot, ErrorCodes.AuthUnavailable, "Server chưa cấu hình Firebase API key (FIREBASE_API_KEY)", countFailure: false);
            return;
        }

        _log.Step(TagForgot, $"Nhờ Firebase gửi email đặt lại mật khẩu tới {addr} ...");
        var r = await _idp.SendPasswordResetAsync(addr, CancellationToken.None);
        if (!r.Ok)
        {
            _forgotAt.TryRemove(key, out _);       // gửi lỗi thì không bắt người dùng chờ
            Fail(conn, msg, TagForgot, r.Code, r.Message, countFailure: r.Code != ErrorCodes.AuthUnavailable);
            return;
        }

        conn.Enqueue(Message.Create(MessageTypes.Ack, new AuthAckPayload(r.Message), msg.ReqId).ToJson());
        _log.Ok(TagForgot, $"Firebase đã gửi email đặt lại mật khẩu tới {addr}");
    }

    // ------------------------------------------------------------ đăng xuất

    public void OnLogout(Connection conn, Session session, Message msg)
    {
        _log.Info(TagLogout, $"{conn.RemoteEndPoint} yêu cầu đăng xuất: \"{session.PlayerName}\" (playerId {session.PlayerId[..8]})");

        var room = _rooms.FindByPlayer(session.PlayerId);
        if (room != null)
        {
            _log.Step(TagLogout, $"Đang ở phòng {room.Code} ({room.Status}) -> báo phòng xử lý rời phòng/rời ván");
            room.Inbox.Writer.TryWrite(new MemberLoggedOut(session.PlayerId));
        }

        _sessions.Remove(session, "người chơi đăng xuất");     // token chết ngay, resumeToken không dùng lại được
        session.Conn = null;
        conn.Session = null;                                    // kết nối quay về trạng thái chưa đăng nhập

        conn.Enqueue(Message.Create(MessageTypes.Ack, new AuthAckPayload("Đã đăng xuất, token đã bị thu hồi"), msg.ReqId).ToJson());
        _log.Ok(TagLogout, $"Đăng xuất thành công: \"{session.PlayerName}\". Số phiên còn lại: {_sessions.Count}");
    }

    // ------------------------------------------------------------ dùng chung

    /// <summary>Kiểm tra payload + nhờ Firebase xác minh idToken. null nếu thất bại (đã trả lỗi cho client).</summary>
    async Task<IdentityUser?> VerifyAsync(Connection conn, Message msg, string tag, string? idToken)
    {
        if (string.IsNullOrWhiteSpace(idToken))
        {
            Fail(conn, msg, tag, ErrorCodes.BadPayload, "Thiếu idToken");
            return null;
        }
        if (_idp == null)
        {
            Fail(conn, msg, tag, ErrorCodes.AuthUnavailable, "Server chưa cấu hình Firebase API key (FIREBASE_API_KEY)", countFailure: false);
            return null;
        }

        _log.Step(tag, $"Xác minh idToken Firebase {ServerLogger.Mask(idToken)} ...");
        var r = await _idp.VerifyIdTokenAsync(idToken, CancellationToken.None);
        if (!r.Ok || r.User == null)
        {
            Fail(conn, msg, tag, r.Code, r.Message, countFailure: r.Code != ErrorCodes.AuthUnavailable);
            return null;
        }

        var u = r.User;
        var exp = u.TokenExpiresAt?.ToLocalTime().ToString("HH:mm:ss dd/MM") ?? "?";
        _log.Step(tag, $"idToken hợp lệ: uid={u.Uid}, email={u.Email}, emailVerified={u.EmailVerified}, idToken hết hạn {exp}");

        if (conn.IsClosed)
        {
            _log.Warn(tag, $"{conn.RemoteEndPoint} đã ngắt kết nối trong lúc xác minh -> hủy");
            return null;
        }
        return u;
    }

    /// <summary>Gắn kết nối vào phiên của tài khoản: tái sử dụng (xoay token, đá kết nối cũ) hoặc cấp phiên mới.</summary>
    Session Establish(Connection conn, AccountRecord account)
    {
        var existing = _sessions.FindByAccount(account.Uid);
        if (existing == null)
        {
            var created = _sessions.Create(account.Username, account.Uid, account.Email);
            created.Conn = conn;
            conn.Session = created;
            return created;
        }

        var old = existing.Conn;
        _sessions.Rotate(existing);                  // token cũ chết, token mới được cấp
        existing.Conn = conn;
        existing.State = ConnState.Connected;
        conn.Session = existing;

        if (old != null && !ReferenceEquals(old, conn))
        {
            _log.Warn(TagLogin, $"\"{existing.PlayerName}\" đăng nhập ở nơi khác -> ngắt kết nối cũ {old.RemoteEndPoint}");
            old.Session = null;
            old.Enqueue(Message.Create(MessageTypes.Error,
                new ErrorPayload(ErrorCodes.SessionReplaced, "Tài khoản vừa đăng nhập ở nơi khác"), null).ToJson());
            _ = Task.Delay(300).ContinueWith(_ => old.Close(), TaskScheduler.Default);   // chờ tin nhắn kịp gửi
        }
        else if (old == null)
        {
            _rooms.FindByPlayer(existing.PlayerId)?.Inbox.Writer.TryWrite(new ConnectionRestored(existing.PlayerId));
        }
        return existing;
    }

    static void SendWelcome(Connection conn, Message req, Session s) =>
        conn.Enqueue(Message.Create(MessageTypes.Welcome,
            new WelcomePayload(s.PlayerId, s.ResumeToken, s.PlayerName, s.Email,
                (int)Math.Max(0, (s.ExpiresAtUtc - DateTime.UtcNow).TotalSeconds)),
            req.ReqId).ToJson());

    void Fail(Connection conn, Message msg, string tag, string code, string text, bool countFailure = true)
    {
        _log.Error(tag, $"{conn.RemoteEndPoint} bị từ chối [{code}] {text}");
        conn.Enqueue(Message.Create(MessageTypes.Error, new ErrorPayload(code, text), msg.ReqId).ToJson());
        if (!countFailure) return;

        conn.AuthFailures++;
        if (conn.AuthFailures >= Protocol.MaxAuthFailures)
        {
            _log.Warn(tag, $"{conn.RemoteEndPoint} thất bại {conn.AuthFailures} lần liên tiếp -> đóng kết nối");
            _ = Task.Delay(300).ContinueWith(_ => conn.Close(), TaskScheduler.Default);
        }
    }

    static string PickName(IdentityUser u)
    {
        var name = !string.IsNullOrWhiteSpace(u.DisplayName) ? u.DisplayName.Trim()
                 : u.Email.Contains('@') ? u.Email.Split('@')[0]
                 : "Player";
        return name.Length > Protocol.MaxPlayerNameLength ? name[..Protocol.MaxPlayerNameLength] : name;
    }

    static bool IsValidEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Length > 254) return false;
        try { return new MailAddress(email).Address == email; }
        catch (FormatException) { return false; }
    }
}
