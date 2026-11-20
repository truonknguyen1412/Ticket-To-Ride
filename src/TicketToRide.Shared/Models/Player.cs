using TicketToRide.Shared.Enums;

namespace TicketToRide.Shared.Models;

public class Player
{
    public Guid MatchPlayerId { get; set; }
    public Guid MatchId { get; set; }
    public Guid AccountId { get; set; }
    public string TrainColor { get; set; } = string.Empty;
    public int RouteScore { get; set; }
    public int RemainingTrains { get; set; } = 45;
    public PlayerStatus PlayerStatus { get; set; } = PlayerStatus.Active;
    public DateTime JoinedAtUtc { get; set; }
    public List<TrainCard> HandCards { get; set; } = new();
    public List<DestinationTicket> DestinationTickets { get; set; } = new();

    public void AddTrainCard(TrainCard card) => HandCards.Add(card);

    public void RemoveTrainCards(IEnumerable<TrainCard> cards)
    {
        var idsToRemove = cards.Select(c => c.CardId).ToHashSet();
        HandCards.RemoveAll(c => idsToRemove.Contains(c.CardId));
    }

    public void AddDestinationTicket(DestinationTicket ticket) => DestinationTickets.Add(ticket);

    public void UpdateRouteScore(int points) => RouteScore += points;

    public void UpdateRemainingTrains(int count) => RemainingTrains = count;
}