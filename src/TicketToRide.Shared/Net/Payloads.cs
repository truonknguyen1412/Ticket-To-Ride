namespace TicketToRide.Shared.Net;

// --- 1. Xác thực & Hệ thống ---
public record HelloPayload(string? PlayerName, string? ResumeToken, string? MapVersion, string? IdToken = null);
public record WelcomePayload(string PlayerId, string ResumeToken);
public record AckPayload(string? RoomCode);
public record ErrorPayload(string Code, string Message);
public record PlayerConnectionPayload(string PlayerId, string Status);

// --- 2. Sảnh chờ (Lobby) ---
public record JoinRoomPayload(string? RoomCode);
public record SetReadyPayload(bool Ready);
public record PlayerPublic(string PlayerId, string Name, string Status, bool Ready, bool IsHost);
public record RoomUpdatedPayload(string RoomCode, string Status, string HostId, List<PlayerPublic> Players);
public record RoomSummary(string RoomCode, string HostName, int PlayerCount, int MaxPlayers, string Status);
public record RoomListPayload(List<RoomSummary> Rooms);

// --- 3. Thao tác trong ván (Client -> Server) ---
/// <param name="SlotIndex">-1: rút từ chồng bài úp; 0..4: rút từ 5 lá bài ngửa.</param>
public record DrawTrainCardPayload(int SlotIndex);
public record SelectTicketsPayload(List<string> KeptTicketIds);
public record ClaimRoutePayload(string RouteId, string CardColorUsed, int LocomotiveCount);

// --- 4. Đồng bộ trạng thái & Kết thúc (Server -> Client) ---
public record PlayerMatchState(
    string PlayerId,
    string Name,
    string Color,
    int RemainingTrains,
    int Score,
    int HandCardCount,
    int TicketCount,
    bool IsConnected);

public record GameSnapshotPayload(
    int StateVersion,
    string CurrentTurnPlayerId,
    bool IsLastRound,
    int DeckCount,
    List<string> FaceUpCards,
    Dictionary<string, string> ClaimedRoutes, // RouteId -> PlayerId
    List<PlayerMatchState> Players,
    List<string> MyHandCards,
    List<string> MyTicketIds);

public record PlayerFinalScore(
    string PlayerId,
    string Name,
    int RoutePoints,
    int CompletedTicketPoints,
    int IncompleteTicketPenalty,
    int LongestPathBonus,
    int TotalScore,
    bool HasLongestPath);

public record GameEndedPayload(string WinnerPlayerId, List<PlayerFinalScore> Rankings);