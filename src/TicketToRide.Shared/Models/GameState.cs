namespace TicketToRide.Shared.Models;

public class GameState
{
    public Guid MatchId { get; set; }
    public List<Player> Players { get; set; } = new();
    public List<Route> Routes { get; set; } = new();
    public List<TrainCard> TrainCardDeck { get; set; } = new();
    public List<TrainCard> FaceUpCards { get; set; } = new();
    public List<TrainCard> TrainCardDiscardPile { get; set; } = new();
    public List<DestinationTicket> DestinationTicketDeck { get; set; } = new();
    public List<DestinationTicket> DestinationTicketDiscardPile { get; set; } = new();
    public Guid CurrentPlayerId { get; set; }
    public int TurnNumber { get; set; }
    public bool IsFinalRound { get; set; }
    public int RemainingFinalTurns { get; set; }
    public long StateVersion { get; set; }

    public Player GetCurrentPlayer() =>
        Players.First(p => p.MatchPlayerId == CurrentPlayerId);

    public void MoveToNextPlayer()
    {
        if (Players.Count == 0) return;
        int currentIndex = Players.FindIndex(p => p.MatchPlayerId == CurrentPlayerId);
        int nextIndex = (currentIndex + 1) % Players.Count;
        CurrentPlayerId = Players[nextIndex].MatchPlayerId;
    }

    public void StartFinalRound()       
    {
        if (!IsFinalRound)
        {
            IsFinalRound = true;
            RemainingFinalTurns = Players.Count;
        }
    }

    public bool IsGameEnded() => IsFinalRound && RemainingFinalTurns <= 0;

    public void IncrementStateVersion() => StateVersion++;
}