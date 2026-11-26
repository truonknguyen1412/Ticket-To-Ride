namespace TicketToRide.Server;

/// <summary>Trạng thái kết nối người chơi (mục 4). Chỉ vòng lặp của phòng được đổi.</summary>
public enum ConnState { Connected, Disconnected, AwaitingConfirm, LeftMatch }

public sealed class Session
{
    public Session(
        string playerId,
        string playerName,
        string resumeToken,
        string? accountId = null,
        string? email = null,
        Guid? dbAccountId = null)
    {
        PlayerId = playerId;
        PlayerName = playerName;
        ResumeToken = resumeToken;
        AccountId = accountId;
        Email = email;
        DbAccountId = dbAccountId;
        IssuedAtUtc = DateTime.UtcNow;
    }

    public string PlayerId { get; }
    public string PlayerName { get; }

    /// <summary>Uid của Firebase (cột Uid trong dbo.Accounts); null nếu phiên tạo bằng HELLO ở chế độ --dev.</summary>
    public string? AccountId { get; }

    /// <summary>Khóa chính AccountId (UNIQUEIDENTIFIER) trong dbo.Accounts, dùng để ghi Rooms / Matches / MatchPlayers.</summary>
    public Guid? DbAccountId { get; }

    public string? Email { get; }

    /// <summary>Token phiên do SERVER cấp (khác idToken của Firebase). Chỉ SessionManager được đổi (xoay token).</summary>
    public string ResumeToken { get; internal set; }
    public DateTime IssuedAtUtc { get; }
    public DateTime ExpiresAtUtc { get; internal set; }

    public Connection? Conn { get; set; }
    public ConnState State { get; set; } = ConnState.Connected;
    public string? RoomCode { get; set; }
    public int RankPoints { get; set; } = 100;
}