using GameServer.Shared.Models;

namespace GameServer.Application.Models
{
    public class Player
    {
        public string Id { get; }
        public string Name { get; }
        public Deck Deck { get; }
        public string? CurrentMapId { get; private set; }
        public Position? Position { get; private set; }
        public string? CurrentFightId { get; private set; }
        public bool IsInFight => CurrentFightId != null;

        public Player(string id, string name)
        {
            Id = id;
            Name = name;
            Deck = new Deck();
            
            // Initialize deck with 25 random cards
            var allCards = CardLibrary.AllCards.ToList();
            var random = new Random();
            while (Deck.Cards.Count < 25)
            {
                var randomCard = allCards[random.Next(allCards.Count())];
                Deck.AddCard(randomCard);
            }
        }

        public void JoinMap(string mapId, Position position)
        {
            CurrentMapId = mapId;
            Position = position;
        }

        public void LeaveMap()
        {
            CurrentMapId = null;
            Position = null;
        }

        public void UpdatePosition(Position newPosition)
        {
            Position = newPosition;
        }

        public void EnterFight(string fightId)
        {
            if (IsInFight)
                throw new InvalidOperationException("Player is already in a fight");
            CurrentFightId = fightId;
        }

        public void LeaveFight()
        {
            CurrentFightId = null;
        }
    }
}
