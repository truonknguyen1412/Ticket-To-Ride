using TicketToRide.Shared.Enums;

namespace TicketToRide.Shared.Models;

public class Room
{
    public Guid RoomId { get; set; }
    public Guid HostAccountId { get; set; }
    public string RoomName { get; set; } = string.Empty;
    public RoomStatus Status { get; set; } = RoomStatus.Waiting;
    public int MaxPlayers { get; set; } = 4;
    public List<RoomPlayer> Players { get; set; } = new();

    public bool IsFull() => Players.Count >= MaxPlayers;

    public bool CanJoin() => Status == RoomStatus.Waiting && !IsFull();

    public void AddPlayer(RoomPlayer player)
    {
        if (CanJoin() && !Players.Any(p => p.AccountId == player.AccountId))
        {
            Players.Add(player);
        }
    }

    public void RemovePlayer(Guid accountId)
    {
        Players.RemoveAll(p => p.AccountId == accountId);
    }

    public bool CanStartMatch() =>
        Status == RoomStatus.Waiting && Players.Count >= 2 && Players.Count <= MaxPlayers;

    public void TransferHost()
    {
        var nextHost = Players.OrderBy(p => p.JoinedAtUtc).FirstOrDefault();
        if (nextHost != null)
        {
            HostAccountId = nextHost.AccountId;
        }
        else
        {
            Status = RoomStatus.Closed;
        }
    }
}