using TicketToRide.Shared;
using TicketToRide.Shared.Net;

namespace TicketToRide.Server;

/// <summary>
/// Kiểm tra phiên bản, schema, hướng gửi; bắt HELLO trước mọi thông điệp khác;
/// đẩy lệnh vào hàng đợi của đúng một phòng. Không đổi trạng thái ván.
/// </summary>
public sealed class Dispatcher
{
    readonly SessionManager _sessions;
    readonly RoomManager _rooms;

    public Dispatcher(SessionManager sessions, RoomManager rooms)
    {
        _sessions = sessions;
        _rooms = rooms;
    }

    public void Handle(Connection conn, string json)
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
            if (msg.Type != MessageTypes.Hello)
                Reply(conn, msg.ReqId, ErrorCodes.HelloRequired, "Phải gửi HELLO trước");
            else
                OnHello(conn, msg);
            return;
        }

        switch (msg.Type)
        {
            case MessageTypes.Hello:
                Reply(conn, msg.ReqId, ErrorCodes.AlreadyHello, "Đã HELLO rồi");
                break;
            case MessageTypes.Ping:
                conn.Enqueue(Message.Create(MessageTypes.Pong, null, msg.ReqId).ToJson());
                break;
            case MessageTypes.Bye:
                conn.Close();                       // xử lý như mất kết nối
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

    void OnHello(Connection conn, Message msg)
    {
        var p = msg.ReadPayload<HelloPayload>();
        if (p == null) { Reply(conn, msg.ReqId, ErrorCodes.BadPayload, "HELLO thiếu payload"); return; }
        // TODO: so mapVersion với map.json của server khi có

        // Nối lại bằng resumeToken
        if (!string.IsNullOrWhiteSpace(p.ResumeToken))
        {
            var existing = _sessions.FindByToken(p.ResumeToken);
            if (existing == null)
            {
                Reply(conn, msg.ReqId, ErrorCodes.ResumeRejected, "resumeToken không còn hiệu lực");
                return;
            }

            var old = existing.Conn;
            existing.Conn = conn;
            conn.Session = existing;
            old?.Close();                           // kết nối cũ (nếu còn) bị thay thế

            SendWelcome(conn, msg, existing);
            _rooms.FindByPlayer(existing.PlayerId)?.Inbox.Writer.TryWrite(new ConnectionRestored(existing.PlayerId));
            return;
        }

        // Vào mới
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
    }

    static void SendWelcome(Connection conn, Message req, Session s) =>
        conn.Enqueue(Message.Create(MessageTypes.Welcome,
            new WelcomePayload(s.PlayerId, s.ResumeToken), req.ReqId).ToJson());

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
