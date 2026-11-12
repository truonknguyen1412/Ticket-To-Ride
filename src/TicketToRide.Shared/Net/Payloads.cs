namespace TicketToRide.Shared.Net;

public record HelloPayload(string? PlayerName, string? ResumeToken, string? MapVersion); // TODO: kiểm tra mapVersion khi có map.json
public record WelcomePayload(string PlayerId, string ResumeToken);
public record JoinRoomPayload(string? RoomCode);
public record SetReadyPayload(bool Ready);
public record AckPayload(string? RoomCode);
public record ErrorPayload(string Code, string Message);

public record PlayerPublic(string PlayerId, string Name, string Status, bool Ready, bool IsHost);
public record RoomUpdatedPayload(string RoomCode, string Status, string HostId, List<PlayerPublic> Players);
public record PlayerConnectionPayload(string PlayerId, string Status);
