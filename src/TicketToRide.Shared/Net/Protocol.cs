namespace TicketToRide.Shared.Net;

/// <summary>Hằng số lấy từ tài liệu kiến trúc Client–Server và vòng đời phòng.</summary>
public static class Protocol
{
    public const int Version = 1;                    // TODO: khớp phiên bản trong tài liệu giao thức
    public const int MaxFrameBytes = 1_048_576;      // mục 7, STT 4
    public const int HeartbeatSeconds = 15;          // client gửi PING mỗi 15 giây
    public const int DeadAfterSeconds = 45;          // 45 giây không hoạt động => mất kết nối
    public const int LobbyGraceSeconds = 30;         // giữ chỗ ở phòng chờ
    public const int OutboxCapacity = 256;           // hàng đợi ghi mỗi Connection
    public const int MinPlayers = 2;
    public const int MaxPlayers = 4;
    public const int MaxProcessedPerPlayer = 200;    // cache reqId đã xử lý (mục 5)
    public const int TokenLifetimeHours = 12;        // resumeToken của server sống tối đa 12 giờ (gia hạn khi nối lại)
    public const int MaxAuthFailures = 5;            // sai quá 5 lần trên một kết nối thì server đóng kết nối
    public const int ForgotCooldownSeconds = 60;     // mỗi email chỉ được xin đặt lại MK 1 lần / 60 giây
    public const int MaxPlayerNameLength = 20;       // [Giả định] validate đầu vào
}

/// <summary>
/// Tên thông điệp. Các tên có trong tài liệu: HELLO, PING, BYE, ACK, ERROR, CREATE_ROOM,
/// START_GAME, LEAVE_ROOM, ROOM_UPDATED, PLAYER_CONNECTION, SYNC_REQUEST.
/// [Giả định - cần khớp giao thức]: WELCOME, PONG, JOIN_ROOM, SET_READY.
/// </summary>
public static class MessageTypes
{
    public const string Hello = "HELLO";
    public const string Welcome = "WELCOME";          // [Giả định]
    public const string Ping = "PING";
    public const string Pong = "PONG";                // [Giả định]
    public const string Bye = "BYE";
    public const string Ack = "ACK";
    public const string Error = "ERROR";
    public const string CreateRoom = "CREATE_ROOM";
    public const string JoinRoom = "JOIN_ROOM";       // [Giả định]
    public const string SetReady = "SET_READY";       // [Giả định]
    public const string StartGame = "START_GAME";
    public const string LeaveRoom = "LEAVE_ROOM";
    public const string RoomUpdated = "ROOM_UPDATED";
    public const string PlayerConnection = "PLAYER_CONNECTION";
    public const string SyncRequest = "SYNC_REQUEST";

    // Xác thực (mở rộng so với tài liệu giao thức): chạy trước khi có phiên
    public const string AuthRegister = "AUTH_REGISTER";   // idToken Firebase + username -> WELCOME
    public const string AuthLogin = "AUTH_LOGIN";         // idToken Firebase -> WELCOME
    public const string AuthForgot = "AUTH_FORGOT";       // email -> ACK (server nhờ Firebase gửi mail đặt lại MK)
    public const string AuthLogout = "AUTH_LOGOUT";       // thu hồi token của phiên hiện tại -> ACK
}

/// <summary>Mã lỗi. Có trong tài liệu: ROOM_IN_GAME, GAME_ENDED, RESUME_REJECTED, INTERNAL_ERROR. Còn lại là [Giả định].</summary>
public static class ErrorCodes
{
    public const string RoomInGame = "ROOM_IN_GAME";
    public const string GameEnded = "GAME_ENDED";
    public const string ResumeRejected = "RESUME_REJECTED";
    public const string InternalError = "INTERNAL_ERROR";

    public const string BadMessage = "BAD_MESSAGE";
    public const string UnsupportedVersion = "UNSUPPORTED_VERSION";
    public const string HelloRequired = "HELLO_REQUIRED";
    public const string AlreadyHello = "ALREADY_HELLO";
    public const string UnknownType = "UNKNOWN_TYPE";
    public const string BadPayload = "BAD_PAYLOAD";
    public const string RoomNotFound = "ROOM_NOT_FOUND";
    public const string RoomFull = "ROOM_FULL";
    public const string NameTaken = "NAME_TAKEN";
    public const string NotHost = "NOT_HOST";
    public const string NotEnoughPlayers = "NOT_ENOUGH_PLAYERS";
    public const string NotAllReady = "NOT_ALL_READY";
    public const string NotInRoom = "NOT_IN_ROOM";
    public const string AlreadyInRoom = "ALREADY_IN_ROOM";

    // Xác thực
    public const string AuthRequired = "AUTH_REQUIRED";          // chưa đăng nhập mà gửi lệnh cần phiên
    public const string AlreadyLoggedIn = "ALREADY_LOGGED_IN";
    public const string TokenInvalid = "TOKEN_INVALID";          // idToken Firebase sai/hết hạn/bị thu hồi
    public const string TokenExpired = "TOKEN_EXPIRED";          // resumeToken của server hết hạn
    public const string AccountExists = "ACCOUNT_EXISTS";
    public const string EmailInvalid = "EMAIL_INVALID";
    public const string TooManyAttempts = "TOO_MANY_ATTEMPTS";
    public const string Cooldown = "COOLDOWN";
    public const string SessionReplaced = "SESSION_REPLACED";    // tài khoản đăng nhập ở nơi khác
    public const string AuthUnavailable = "AUTH_UNAVAILABLE";    // server chưa cấu hình/không gọi được Firebase
}

/// <summary>Giá trị trên đường truyền. RECOVERING/CLOSED là nội bộ, không gửi cho client.</summary>
public static class WireStatus
{
    public const string Lobby = "LOBBY";
    public const string InGame = "IN_GAME";
    public const string Ended = "ENDED";

    public const string Connected = "CONNECTED";
    public const string Disconnected = "DISCONNECTED";
    public const string LeftMatch = "LEFT_MATCH";
}
