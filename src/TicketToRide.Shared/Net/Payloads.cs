namespace TicketToRide.Shared.Net;

public record HelloPayload(string? PlayerName, string? ResumeToken, string? MapVersion); // TODO: kiểm tra mapVersion khi có map.json
public record WelcomePayload(
    string PlayerId,
    string ResumeToken,
    string? PlayerName = null,
    string? Email = null,
    int? ExpiresInSeconds = null,
    string? ActiveRoomCode = null,
    string? ActiveRoomStatus = null);
public record AuthLoginPayload(string? IdToken);
public record AuthRegisterPayload(string? IdToken, string? Username);
public record AuthForgotPayload(string? Email);
public record AuthAckPayload(string Message);
public record JoinRoomPayload(string? RoomCode);
public record SetReadyPayload(bool Ready);
public record AckPayload(string? RoomCode);
public record ErrorPayload(string Code, string Message);

public record FindMatchPayload(bool IsRanked);
public record MatchStatusPayload(bool IsRanked, int QueuedCount, int NeededPlayers, int ElapsedSeconds);
public record MatchFoundPayload(string RoomCode, bool IsRanked, List<PlayerPublic> Players);
public record ReadyCheckUpdatedPayload(string RoomCode, bool IsRanked, int RemainingSeconds, List<PlayerPublic> Players);
public record MatchPenaltyPayload(int RemainingSeconds, string Message);

public record PlayerPublic(string PlayerId, string Name, string Status, bool Ready, bool IsHost);
public record RoomUpdatedPayload(string RoomCode, string Status, string HostId, List<PlayerPublic> Players);
public record PlayerConnectionPayload(string PlayerId, string Status);