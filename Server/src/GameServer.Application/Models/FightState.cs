using Proto;
using System.Collections.Generic;
using System.Linq;

namespace GameServer.Application.Models
{
    /// <summary>
    /// Manages the state of a card battle between two players, including health points,
    /// action points, card hands, and turn management.
    /// </summary>
    public class FightState
    {
        /// <summary>Starting health points for each player</summary>
        public const int STARTING_HP = 10;
        
        /// <summary>Action points granted at the start of each turn</summary>
        public const int STARTING_ACTION_POINTS = 3;
        
        /// <summary>Maximum number of cards a player can hold in their hand</summary>
        public const int MAX_HAND_SIZE = 10;
        
        /// <summary>Number of cards drawn at the start of each turn</summary>
        public const int CARDS_PER_TURN = 5;

        /// <summary>ID of the player whose turn it is currently</summary>
        public string CurrentTurnPlayerId { get; private set; }
        
        /// <summary>Dictionary mapping player IDs to their current fight states</summary>
        private Dictionary<string, PlayerFightState> PlayerStates { get; }

        /// <summary>
        /// Initializes a new fight state between two players, setting up initial HP and turn order
        /// </summary>
        /// <param name="player1Id">ID of the first player (starts the fight)</param>
        /// <param name="player2Id">ID of the second player</param>
        public FightState(string player1Id, string player2Id)
        {
            CurrentTurnPlayerId = player1Id; // Player 1 starts
            PlayerStates = new Dictionary<string, PlayerFightState>
            {
                { player1Id, new PlayerFightState(STARTING_HP) },
                { player2Id, new PlayerFightState(STARTING_HP) }
            };
        }

        /// <summary>
        /// Checks if it's currently the specified player's turn
        /// </summary>
        public bool IsPlayersTurn(string playerId) => CurrentTurnPlayerId == playerId;

        /// <summary>
        /// Starts a new turn for the specified player, granting action points
        /// </summary>
        /// <param name="playerId">ID of the player whose turn is starting</param>
        /// <exception cref="InvalidOperationException">Thrown if player is not in the fight</exception>
        public void StartTurn(string playerId)
        {
            if (!PlayerStates.ContainsKey(playerId))
                throw new InvalidOperationException($"Player {playerId} not in fight");

            CurrentTurnPlayerId = playerId;
            var state = PlayerStates[playerId];
            state.ActionPoints += STARTING_ACTION_POINTS;
        }

        /// <summary>
        /// Draws cards for the specified player up to their maximum hand size
        /// </summary>
        /// <returns>List of cards that were drawn</returns>
        public List<Card> DrawCards(string playerId, Deck deck)
        {
            var state = PlayerStates[playerId];
            var cardsToDraw = Math.Min(CARDS_PER_TURN, MAX_HAND_SIZE - state.Hand.Count);
            var drawnCards = deck.DrawCards(cardsToDraw);
            state.Hand.AddRange(drawnCards);
            return drawnCards;
        }

        /// <summary>
        /// Checks if a player can play a specific card based on action points and card possession
        /// </summary>
        public bool CanPlayCard(string playerId, Card card)
        {
            var state = PlayerStates[playerId];
            return state.ActionPoints >= card.Cost && state.Hand.Contains(card);
        }

        /// <summary>
        /// Plays a card from a player's hand, consuming action points
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if the card cannot be played</exception>
        public void PlayCard(string playerId, Card card)
        {
            var state = PlayerStates[playerId];
            if (!CanPlayCard(playerId, card))
                throw new InvalidOperationException("Cannot play this card");

            state.ActionPoints -= card.Cost;
            state.Hand.Remove(card);
        }
        
        /// <summary>
        /// Discards all cards from a player's hand to their discard pile
        /// </summary>
        /// <param name="playerId">ID of the player whose hand to discard</param>
        /// <param name="deck">The player's deck to discard cards to</param>
        public void DiscardHand(string playerId, Deck deck)
        {
            var state = PlayerStates[playerId];
            foreach (var card in state.Hand.ToList())
            {
                deck.DiscardCard(card);
            }
            state.Hand.Clear();
        }

        /// <summary>
        /// Applies damage to a player, ensuring HP doesn't go below 0
        /// </summary>
        public void ApplyDamage(string targetPlayerId, int amount)
        {
            var state = PlayerStates[targetPlayerId];
            state.HitPoints = Math.Max(0, state.HitPoints - amount);
        }

        /// <summary>
        /// Heals a player, ensuring HP doesn't exceed starting HP
        /// </summary>
        public void ApplyHealing(string targetPlayerId, int amount)
        {
            var state = PlayerStates[targetPlayerId];
            state.HitPoints = Math.Min(STARTING_HP, state.HitPoints + amount);
        }

        /// <summary>
        /// Checks if the fight is over (any player has 0 or less HP)
        /// </summary>
        public bool IsGameOver => PlayerStates.Any(p => p.Value.HitPoints <= 0);

        /// <summary>
        /// Gets the ID of the winning player, or null if the game isn't over
        /// </summary>
        public string GetWinnerId()
        {
            if (!IsGameOver) return null;
            return PlayerStates.First(p => p.Value.HitPoints > 0).Key;
        }

        /// <summary>
        /// Retrieves the current fight state for a specific player
        /// </summary>
        public PlayerFightState GetPlayerState(string playerId) => PlayerStates[playerId];
    }

    /// <summary>
    /// Tracks an individual player's state during a fight, including their
    /// current HP, action points, and cards in hand
    /// </summary>
    public class PlayerFightState
    {
        /// <summary>Current health points of the player</summary>
        public int HitPoints { get; set; }
        
        /// <summary>Current action points available for playing cards</summary>
        public int ActionPoints { get; set; }
        
        /// <summary>Cards currently in the player's hand</summary>
        public List<Card> Hand { get; }

        /// <summary>
        /// Initializes a new player state with starting HP and empty hand
        /// </summary>
        public PlayerFightState(int startingHp)
        {
            HitPoints = startingHp;
            ActionPoints = 0;
            Hand = new List<Card>();
        }
    }
}
