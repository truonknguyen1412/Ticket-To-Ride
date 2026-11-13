using TicketToRide.Shared;
using TicketToRide.Shared.Net;

namespace TicketToRide.Server;

/// <summary>
/// Kiểm tra phiên bản, schema, chặn thông điệp khi chưa đăng nhập; giao 4 luồng xác thực cho <see cref="AuthService"/>;
/// đẩy lệnh vào hàng đợi của đúng một phòng. Không đổi trạng thái phòng.
/// </summary>
public sealed class Dispatcher
{
    const string Tag = "PHIEN";

    readonly SessionManager _sessions;
    readonly RoomManager _rooms;
    readonly AuthService _auth;
    readonly ServerOptions _options;
    readonly ServerLogger _log;

    public Dispatcher(SessionManager sessions, RoomManager rooms, AuthService auth, ServerOptions options, ServerLogger log)
    {
        _sessions = sessions;
        _rooms = rooms;
        _auth = auth;
        _options = options;
        _log = log;
    }

    public async Task Handle(Connection conn, string json)
    {
        try
        {
            await HandleCore(conn, json);
        }
        catch (Exception ex)
        {
            _log.Error("LOI", $"{conn.RemoteEndPoint} lỗi nội bộ khi xử lý thông điệp: {ex.Message}");
            Reply(conn, null, ErrorCodes.InternalError, "Lỗi nội bộ của server");
        }
    }

    async Task HandleCore(Connection conn, string json)
    {
        var msg = Message.Parse(json);
        if (msg == null || string.IsNullOrEmpty(msg.Type))
        {
            Reply(conn, null, ErrorCodes.BadMessage, "Thông điệp không hợp lệ");
            return;
        }
        if (msg.Version != Protocol.Version)
        {
            Reply(conn, msg.ReqId, ErrorCodes.UnsupportedVersion, $"Server dùng phiên bản giao thức {Protocol.Version}");
            return;
        }

        var session = conn.Session;
        if (session == null)
        {
            // Chưa đăng nhập: chỉ cho phép xác thực, HELLO (nối lại bằng token / --dev), PING, BYE
            switch (msg.Type)
            {
                case MessageTypes.AuthRegister: await _auth.OnRegisterAsync(conn, msg); break;
                case MessageTypes.AuthLogin: await _auth.OnLoginAsync(conn, msg); break;
                case MessageTypes.AuthForgot: await _auth.OnForgotAsync(conn, msg); break;
                case MessageTypes.Hello: OnHello(conn, msg); break;
                case MessageTypes.Ping: conn.Enqueue(Message.Create(MessageTypes.Pong, null, msg.ReqId).ToJson()); break;
                case MessageTypes.Bye: conn.Close(); break;
                default:
                    _log.Warn(Tag, $"{conn.RemoteEndPoint} gửi {msg.Type} khi chưa đăng nhập -> từ chối");
                    Reply(conn, msg.ReqId, ErrorCodes.AuthRequired, "Phải đăng nhập trước");
                    break;
            }
            return;
        }

        switch (msg.Type)
        {
            case MessageTypes.Hello:
                Reply(conn, msg.ReqId, ErrorCodes.AlreadyHello, "Đã HELLO rồi");
                break;
            case MessageTypes.AuthLogin:
            case MessageTypes.AuthRegister:
            case MessageTypes.AuthForgot:
                Reply(conn, msg.ReqId, ErrorCodes.AlreadyLoggedIn, "Đang đăng nhập, hãy đăng xuất trước");
                break;
            case MessageTypes.AuthLogout:
                _auth.OnLogout(conn, session, msg);
                break;
            case MessageTypes.Ping:
                _sessions.Renew(session);               // còn hoạt động thì token còn sống
                conn.Enqueue(Message.Create(MessageTypes.Pong, null, msg.ReqId).ToJson());
                break;
            case MessageTypes.Bye:
                conn.Close();                           // xử lý như mất kết nối; muốn thu hồi token phải gửi AUTH_LOGOUT
                break;
            case MessageTypes.CreateRoom:
                OnCreateRoom(conn, session, msg);
                break;
            case MessageTypes.JoinRoom:
                OnJoinRoom(conn, session, msg);
                break;
            case MessageTypes.SetReady:
            case MessageTypes.StartGame:
            case MessageTypes.LeaveRoom:
                Forward(conn, session, msg);
                break;
            default:
                Reply(conn, msg.ReqId, ErrorCodes.UnknownType, $"Không hỗ trợ thông điệp {msg.Type}");
                break;
        }
    }

    /// <summary>HELLO: nối lại bằng resumeToken (luôn được phép) hoặc vào mới chỉ bằng tên (chỉ khi chạy --dev).</summary>
    void OnHello(Connection conn, Message msg)
    {
        var p = msg.ReadPayload<HelloPayload>();
        if (p == null) { Reply(conn, msg.ReqId, ErrorCodes.BadPayload, "HELLO thiếu payload"); return; }
        // TODO: so mapVersion với map.json của server khi có

        // Nối lại bằng resumeToken
        if (!string.IsNullOrWhiteSpace(p.ResumeToken))
        {
            var token = p.ResumeToken;
            var existing = _sessions.FindByToken(token);
            if (existing == null)
            {
                _log.Error(Tag, $"{conn.RemoteEndPoint} nối lại bằng token {ServerLogger.Mask(token)}: không tồn tại hoặc đã bị thu hồi");
                Reply(conn, msg.ReqId, ErrorCodes.ResumeRejected, "resumeToken không còn hiệu lực");
                return;
            }
            if (_sessions.IsExpired(existing))
            {
                _log.Error(Tag, $"{conn.RemoteEndPoint} nối lại bằng token {ServerLogger.Mask(token)} của \"{existing.PlayerName}\": token đã hết hạn");
                _rooms.FindByPlayer(existing.PlayerId)?.Inbox.Writer.TryWrite(new MemberLoggedOut(existing.PlayerId));
                _sessions.Remove(existing, "token hết hạn khi nối lại");
                Reply(conn, msg.ReqId, ErrorCodes.TokenExpired, "resumeToken đã hết hạn, hãy đăng nhập lại");
                return;
            }

            var old = existing.Conn;
            existing.Conn = conn;
            conn.Session = existing;
            _sessions.Renew(existing);
            if (old != null && !ReferenceEquals(old, conn)) { old.Session = null; old.Close(); }   // kết nối cũ (nếu còn) bị thay thế

            SendWelcome(conn, msg, existing);
            _log.Ok(Tag, $"\"{existing.PlayerName}\" nối lại bằng token {ServerLogger.Mask(token)} từ {conn.RemoteEndPoint}, gia hạn đến {ServerLogger.Time(existing.ExpiresAtUtc)}");
            _rooms.FindByPlayer(existing.PlayerId)?.Inbox.Writer.TryWrite(new ConnectionRestored(existing.PlayerId));
            return;
        }

        // Vào mới chỉ bằng tên: chỉ khi --dev
        if (!_options.AllowAnonymous)
        {
            _log.Warn(Tag, $"{conn.RemoteEndPoint} HELLO không có token -> từ chối (server yêu cầu AUTH_LOGIN/AUTH_REGISTER)");
            Reply(conn, msg.ReqId, ErrorCodes.AuthRequired, "Server yêu cầu đăng nhập (AUTH_LOGIN hoặc AUTH_REGISTER)");
            return;
        }

        var name = p.PlayerName?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > Protocol.MaxPlayerNameLength)
        {
            Reply(conn, msg.ReqId, ErrorCodes.BadPayload, $"Tên người chơi dài 1–{Protocol.MaxPlayerNameLength} ký tự");
            return;
        }

        var session = _sessions.Create(name);
        session.Conn = conn;
        conn.Session = session;
        SendWelcome(conn, msg, session);
        _log.Warn(Tag, $"[--dev] \"{name}\" vào bằng HELLO không xác thực từ {conn.RemoteEndPoint}");
    }

    static void SendWelcome(Connection conn, Message req, Session s) =>
        conn.Enqueue(Message.Create(MessageTypes.Welcome,
            new WelcomePayload(s.PlayerId, s.ResumeToken, s.PlayerName, s.Email,
                (int)Math.Max(0, (s.ExpiresAtUtc - DateTime.UtcNow).TotalSeconds)),
            req.ReqId).ToJson());

    void OnCreateRoom(Connection conn, Session session, Message msg)
    {
        if (_rooms.FindByPlayer(session.PlayerId) != null)
        {
            Reply(conn, msg.ReqId, ErrorCodes.AlreadyInRoom, "Bạn đang ở trong một phòng");
            return;
        }

        var room = _rooms.Create(session);
        if (room == null)
        {
            Reply(conn, msg.ReqId, ErrorCodes.InternalError, "Không tạo được mã phòng");
            return;
        }
        room.Inbox.Writer.TryWrite(new ClientMessage(session, msg));   // phòng gửi ACK + ROOM_UPDATED
    }

    void OnJoinRoom(Connection conn, Session session, Message msg)
    {
        var p = msg.ReadPayload<JoinRoomPayload>();
        var code = p?.RoomCode?.Trim();
        if (string.IsNullOrEmpty(code)) { Reply(conn, msg.ReqId, ErrorCodes.BadPayload, "Thiếu roomCode"); return; }

        if (_rooms.FindByPlayer(session.PlayerId) != null)
        {
            Reply(conn, msg.ReqId, ErrorCodes.AlreadyInRoom, "Bạn đang ở trong một phòng");
            return;
        }

        var room = _rooms.Find(code);
        if (room == null || !room.Inbox.Writer.TryWrite(new ClientMessage(session, msg)))
            Reply(conn, msg.ReqId, ErrorCodes.RoomNotFound, "Không tìm thấy phòng");
    }

    void Forward(Connection conn, Session session, Message msg)
    {
        var room = _rooms.FindByPlayer(session.PlayerId);
        if (room == null) { Reply(conn, msg.ReqId, ErrorCodes.NotInRoom, "Bạn không ở trong phòng"); return; }
        if (!room.Inbox.Writer.TryWrite(new ClientMessage(session, msg)))
            Reply(conn, msg.ReqId, ErrorCodes.RoomNotFound, "Phòng đã đóng");
    }

    static void Reply(Connection conn, string? reqId, string code, string text) =>
        conn.Enqueue(Message.Create(MessageTypes.Error, new ErrorPayload(code, text), reqId).ToJson());
}
