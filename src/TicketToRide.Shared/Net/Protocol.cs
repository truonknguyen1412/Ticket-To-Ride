namespace TicketToRide.Shared.Net;

/// <summary>Hằng số giao thức Client–Server và vòng đời phòng.</summary>
public static class Protocol
{
    public const int Version = 1;
    public const int MaxFrameBytes = 1_048_576;      // 1 MB
    public const int HeartbeatSeconds = 15;          // Client gửi PING mỗi 15 giây
    public const int DeadAfterSeconds = 45;          // 45 giây không phản hồi => rớt mạng
    public const int LobbyGraceSeconds = 30;         // Thời gian giữ chỗ khi rớt mạng
    public const int OutboxCapacity = 256;
    public const int MinPlayers = 2;
    public const int MaxPlayers = 4;
    public const int MaxProcessedPerPlayer = 200;
    public const int MaxPlayerNameLength = 20;
}

/// <summary>Tên các loại thông điệp truyền giữa Client và Server.</summary>
public static class MessageTypes
{
    // 1. Hệ thống & Xác thực
    public const string Hello = "HELLO";
    public const string Welcome = "WELCOME";
    public const string Ping = "PING";
    public const string Pong = "PONG";
    public const string Bye = "BYE";
    public const string Ack = "ACK";
    public const string Error = "ERROR";
    public const string SyncRequest = "SYNC_REQUEST";

    // 2. Sảnh chờ (Lobby)
    public const string GetRooms = "GET_ROOMS";
    public const string RoomList = "ROOM_LIST";
    public const string CreateRoom = "CREATE_ROOM";
    public const string JoinRoom = "JOIN_ROOM";
    public const string SetReady = "SET_READY";
    public const string StartGame = "START_GAME";
    public const string LeaveRoom = "LEAVE_ROOM";
    public const string RoomUpdated = "ROOM_UPDATED";
    public const string PlayerConnection = "PLAYER_CONNECTION";

    // 3. Trong ván đấu (Gameplay)
    public const string GameStarted = "GAME_STARTED";
    public const string StateUpdate = "STATE_UPDATE";
    public const string GameSnapshot = "GAME_SNAPSHOT";
    public const string DrawTrainCard = "DRAW_TRAIN_CARD";
    public const string DrawTickets = "DRAW_TICKETS";
    public const string SelectTickets = "SELECT_TICKETS";
    public const string ClaimRoute = "CLAIM_ROUTE";
    public const string EndTurn = "END_TURN";
    public const string GameEnded = "GAME_ENDED";
}

/// <summary>Mã lỗi chuẩn hóa Server trả về qua gói ERROR.</summary>
public static class ErrorCodes
{
    // Lỗi xác thực & phiên
    public const string Unauthorized = "UNAUTHORIZED";
    public const string InvalidToken = "INVALID_TOKEN";
    public const string HelloRequired = "HELLO_REQUIRED";
    public const string AlreadyHello = "ALREADY_HELLO";
    public const string ResumeRejected = "RESUME_REJECTED";

    // Lỗi định dạng giao thức
    public const string BadMessage = "BAD_MESSAGE";
    public const string UnsupportedVersion = "UNSUPPORTED_VERSION";
    public const string UnknownType = "UNKNOWN_TYPE";
    public const string BadPayload = "BAD_PAYLOAD";
    public const string InternalError = "INTERNAL_ERROR";

    // Lỗi sảnh chờ
    public const string RoomNotFound = "ROOM_NOT_FOUND";
    public const string RoomFull = "ROOM_FULL";
    public const string RoomInGame = "ROOM_IN_GAME";
    public const string GameEnded = "GAME_ENDED";
    public const string NameTaken = "NAME_TAKEN";
    public const string NotHost = "NOT_HOST";
    public const string NotEnoughPlayers = "NOT_ENOUGH_PLAYERS";
    public const string NotAllReady = "NOT_ALL_READY";
    public const string NotInRoom = "NOT_IN_ROOM";
    public const string AlreadyInRoom = "ALREADY_IN_ROOM";

    // Lỗi luật chơi trong ván
    public const string NotYourTurn = "NOT_YOUR_TURN";
    public const string InvalidAction = "INVALID_ACTION";
    public const string RouteAlreadyClaimed = "ROUTE_ALREADY_CLAIMED";
    public const string InsufficientCards = "INSUFFICIENT_CARDS";
    public const string InsufficientTrains = "INSUFFICIENT_TRAINS";
}

/// <summary>Trạng thái phòng và kết nối trên đường truyền.</summary>
public static class WireStatus
{
    public const string Lobby = "LOBBY";
    public const string InGame = "IN_GAME";
    public const string Ended = "ENDED";

    public const string Connected = "CONNECTED";
    public const string Disconnected = "DISCONNECTED";
    public const string LeftMatch = "LEFT_MATCH";
}