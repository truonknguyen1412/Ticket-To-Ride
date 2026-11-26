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
    readonly MatchmakingService? _matchmaking;
    readonly ConcurrentDictionary<string, DateTime> _forgotAt = new();   // email -> lần xin gần nhất

    public AuthService(SessionManager sessions, RoomManager rooms, AccountStore accounts, IIdentityProvider? idp, ServerLogger log, MatchmakingService? matchmaking = null)
    {
        _sessions = sessions;
        _rooms = rooms;
        _accounts = accounts;
        _idp = idp;
        _log = log;
        _matchmaking = matchmaking;
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

        AccountRecord account;
        try
        {
            var (created, record) = await _accounts.TryCreateAsync(user.Uid, user.Email, username);
            if (!created)
            {
                Fail(conn, msg, TagReg, ErrorCodes.AccountExists, "Tài khoản đã được đăng ký trên server, hãy đăng nhập");
                return;
            }
            account = record;
        }
        catch (Exception ex)
        {
            FailDb(conn, msg, TagReg, ex);
            return;
        }
        _log.Step(TagReg, $"Đã lưu hồ sơ vào SQL Server (bảng Accounts): \"{account.Username}\" <{account.Email}> uid={user.Uid}");

        var (session, activeRoom) = Establish(conn, account);
        conn.AuthFailures = 0;
        SendWelcome(conn, msg, session, activeRoom);
        _log.Ok(TagReg, $"Đăng ký thành công: \"{account.Username}\" <{account.Email}>, tự đăng nhập, token {ServerLogger.Mask(session.ResumeToken)}");
    }

    // ------------------------------------------------------------ đăng nhập

    public async Task OnLoginAsync(Connection conn, Message msg)
    {
        _log.Info(TagLogin, $"{conn.RemoteEndPoint} gửi yêu cầu đăng nhập");
        var p = msg.ReadPayload<AuthLoginPayload>();

        var user = await VerifyAsync(conn, msg, TagLogin, p?.IdToken);
        if (user == null) return;

        AccountRecord account;
        bool created;
        try
        {
            (account, created) = await _accounts.RecordLoginAsync(user.Uid, user.Email, PickName(user));
        }
        catch (Exception ex)
        {
            FailDb(conn, msg, TagLogin, ex);
            return;
        }
        if (created)
            _log.Warn(TagLogin, $"uid={user.Uid} chưa có hồ sơ trên server (tạo trực tiếp ở Firebase) -> tự tạo hồ sơ \"{account.Username}\"");

        var (session, activeRoom) = Establish(conn, account);
        conn.AuthFailures = 0;
        SendWelcome(conn, msg, session, activeRoom);
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

        _matchmaking?.RemovePlayer(session.PlayerId);

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
    // ------------------------------------------------------------ hồ sơ người chơi

    /// <summary>
    /// Đọc điểm Rank và Uy tín của tài khoản đang đăng nhập.
    /// </summary>
    public async Task OnGetProfileAsync(
        Connection conn,
        Session session,
        Message msg)
    {
        // Kiểm tra phiên có gắn với tài khoản trong database hay không.
        if (session.DbAccountId == null)
        {
            conn.Enqueue(Message.Create(
                MessageTypes.Error,
                new ErrorPayload(
                    ErrorCodes.ProfileUnavailable,
                    "Phiên hiện tại không gắn với tài khoản trong cơ sở dữ liệu."
                ),
                msg.ReqId
            ).ToJson());

            return;
        }

        try
        {
            // Lấy thông tin tài khoản từ SQL Server.
            var account = await _accounts.FindByAccountIdAsync(
                session.DbAccountId.Value
            );

            if (account == null)
            {
                conn.Enqueue(Message.Create(
                    MessageTypes.Error,
                    new ErrorPayload(
                        ErrorCodes.ProfileUnavailable,
                        "Không tìm thấy hồ sơ tài khoản."
                    ),
                    msg.ReqId
                ).ToJson());

                return;
            }

            // Đồng bộ điểm Rank đang lưu trong Session.
            session.RankPoints = account.RankPoints;

            // Tạo dữ liệu trả về Client.
            var payload = new ProfileStatsPayload(
                account.RankPoints,
                account.Reputation
            );

            conn.Enqueue(Message.Create(
                MessageTypes.ProfileStats,
                payload,
                msg.ReqId
            ).ToJson());
        }
        catch (Exception ex)
        {
            _log.Error(
                "HO SO",
                $"Không tải được hồ sơ của {session.PlayerName}: {ex.Message}"
            );

            conn.Enqueue(Message.Create(
                MessageTypes.Error,
                new ErrorPayload(
                    ErrorCodes.DbUnavailable,
                    "Server không đọc được dữ liệu hồ sơ từ SQL Server."
                ),
                msg.ReqId
            ).ToJson());
        }
    }
    // ------------------------------------------------------------ dùng chung

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
    (Session Session, Room? ActiveRoom) Establish(Connection conn, AccountRecord account)
    {
        var existing = _sessions.FindByAccount(account.Uid);
        if (existing == null)
        {
            var created = _sessions.Create(account.Username, account.Uid, account.Email, account.AccountId, account.RankPoints);
            created.Conn = conn;
            conn.Session = created;
            return (created, null);
        }

        var old = existing.Conn;
        _sessions.Rotate(existing);                  // token cũ chết, token mới được cấp
        existing.RankPoints = account.RankPoints;
        existing.Conn = conn;
        existing.State = ConnState.Connected;
        conn.Session = existing;

        if (old != null && !ReferenceEquals(old, conn))
        {
            _log.Warn(TagLogin, $"\"{existing.PlayerName}\" đăng nhập ở nơi khác -> ngắt kết nối cũ {old.RemoteEndPoint}");
            old.Session = null;
            old.Enqueue(Message.Create(MessageTypes.Error,
                new ErrorPayload(ErrorCodes.SessionReplaced, "Tài khoản đã được đăng nhập trên một thiết bị khác."), null).ToJson());
            _ = Task.Delay(300).ContinueWith(_ => old.Close(), TaskScheduler.Default);
        }

        var activeRoom = _rooms.FindByPlayer(existing.PlayerId);
        activeRoom?.SendCurrentStateTo(existing);

        return (existing, activeRoom);
    }

    static void SendWelcome(Connection conn, Message req, Session s, Room? activeRoom) =>
        conn.Enqueue(Message.Create(MessageTypes.Welcome,
            new WelcomePayload(
                s.PlayerId,
                s.ResumeToken,
                s.PlayerName,
                s.Email,
                (int)Math.Max(0, (s.ExpiresAtUtc - DateTime.UtcNow).TotalSeconds),
                activeRoom?.Code,
                activeRoom?.WireRoomStatus),
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

    /// <summary>SQL Server lỗi/không kết nối được: báo client, không tính vào số lần sai xác thực.</summary>
    void FailDb(Connection conn, Message msg, string tag, Exception ex)
    {
        _log.Error(tag, $"Lỗi SQL Server: {ex.Message}");
        Fail(conn, msg, tag, ErrorCodes.DbUnavailable,
            "Server không truy cập được cơ sở dữ liệu SQL Server, hãy báo người chạy server", countFailure: false);
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