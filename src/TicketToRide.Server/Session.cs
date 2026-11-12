namespace TicketToRide.Server;

/// <summary>Trạng thái kết nối người chơi (mục 4). Chỉ vòng lặp của phòng được đổi.</summary>
public enum ConnState { Connected, Disconnected, AwaitingConfirm, LeftMatch }

public sealed class Session
{
    public Session(string playerId, string playerName, string resumeToken)
    {
        PlayerId = playerId;
        PlayerName = playerName;
        ResumeToken = resumeToken;
    }

    public string PlayerId { get; }
    public string PlayerName { get; }
    public string ResumeToken { get; }       // TODO (dòng 23): lưu bản băm khi có SQL Server
    public Connection? Conn { get; set; }
    public ConnState State { get; set; } = ConnState.Connected;
    public string? RoomCode { get; set; }
}
