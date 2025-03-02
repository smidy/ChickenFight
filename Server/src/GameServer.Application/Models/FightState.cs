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
        /// Starts a new turn for the specified player, granting action points and processing status effects
        /// </summary>
        /// <param name="playerId">ID of the player whose turn is starting</param>
        /// <exception cref="InvalidOperationException">Thrown if player is not in the fight</exception>
        public void StartTurn(string playerId)
        {
            if (!PlayerStates.ContainsKey(playerId))
                throw new InvalidOperationException($"Player {playerId} not in fight");

            CurrentTurnPlayerId = playerId;
            var state = PlayerStates[playerId];
            
            // Process status effects for the player starting their turn
            state.ProcessStatusEffects(this, playerId);
            
            // Grant action points
            state.ActionPoints += STARTING_ACTION_POINTS;
        }
        
        /// <summary>
        /// Applies a status effect to a player
        /// </summary>
        public void ApplyStatusEffect(string playerId, StatusEffect effect)
        {
            if (!PlayerStates.ContainsKey(playerId))
                throw new InvalidOperationException($"Player {playerId} not in fight");
                
            PlayerStates[playerId].ApplyStatusEffect(effect);
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
        /// Checks if a player can play a specific card based on action points, card possession, and status effects
        /// </summary>
        public bool CanPlayCard(string playerId, Card card)
        {
            var state = PlayerStates[playerId];
            
            // Check if player has the card and enough action points
            bool hasCardAndPoints = state.ActionPoints >= card.Cost && state.Hand.Contains(card);
            if (!hasCardAndPoints) return false;
            
            // Check for card lock effects that might prevent playing this card
            var cardLockEffects = state.ActiveEffects
                .Where(e => e.Type == StatusEffectType.CardLock)
                .ToList();
                
            // In a full implementation, we would check if the locked card type matches this card
            // For now, we'll assume no cards are locked
            
            return true;
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
        /// Applies damage to a player, considering damage boost, reduction, and dodge effects
        /// </summary>
        public void ApplyDamage(string targetPlayerId, int amount, string attackerId = null)
        {
            var state = PlayerStates[targetPlayerId];
            
            // If attacker ID is not provided, find it (the other player)
            if (attackerId == null)
            {
                attackerId = PlayerStates.Keys.First(id => id != targetPlayerId);
            }
            
            var attackerState = PlayerStates[attackerId];
            
            // Apply damage boost effects from attacker
            var damageBoostEffects = attackerState.ActiveEffects
                .Where(e => e.Type == StatusEffectType.DamageBoost)
                .ToList();
                
            float totalBoostPercentage = damageBoostEffects.Sum(e => e.Magnitude) / 100.0f;
            int boostedDamage = amount + (int)(amount * totalBoostPercentage);
            
            // Check for dodge effects on target
            var dodgeEffects = state.ActiveEffects
                .Where(e => e.Type == StatusEffectType.DodgeChance)
                .ToList();
                
            foreach (var effect in dodgeEffects)
            {
                // Simple implementation: if random number is less than dodge chance, avoid damage
                var random = new Random();
                if (random.Next(100) < effect.Magnitude)
                {
                    // Damage completely avoided
                    return;
                }
            }
            
            // Apply damage reduction effects on target
            var damageReductionEffects = state.ActiveEffects
                .Where(e => e.Type == StatusEffectType.DamageReduction)
                .ToList();
                
            int totalReduction = damageReductionEffects.Sum(e => e.Magnitude);
            int actualDamage = Math.Max(0, boostedDamage - totalReduction);
            
            // Apply the damage
            state.HitPoints = Math.Max(0, state.HitPoints - actualDamage);
            
            // Apply damage reflection effects from target back to attacker
            var reflectionEffects = state.ActiveEffects
                .Where(e => e.Type == StatusEffectType.DamageReflection)
                .ToList();
                
            // Apply reflection damage to attacker
            foreach (var effect in reflectionEffects)
            {
                attackerState.HitPoints = Math.Max(0, attackerState.HitPoints - effect.Magnitude);
            }
        }

        /// <summary>
        /// Heals a player, ensuring HP doesn't exceed maximum HP
        /// </summary>
        public void ApplyHealing(string targetPlayerId, int amount)
        {
            var state = PlayerStates[targetPlayerId];
            state.HitPoints = Math.Min(state.MaxHitPoints, state.HitPoints + amount);
        }
        
        /// <summary>
        /// Increases a player's maximum hit points
        /// </summary>
        public void IncreaseMaxHitPoints(string targetPlayerId, int amount)
        {
            var state = PlayerStates[targetPlayerId];
            state.MaxHitPoints += amount;
            
            // Also heal the player by the same amount
            ApplyHealing(targetPlayerId, amount);
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
    /// current HP, action points, cards in hand, and active status effects
    /// </summary>
    public class PlayerFightState
    {
        /// <summary>Current health points of the player</summary>
        public int HitPoints { get; set; }
        
        /// <summary>Maximum health points of the player</summary>
        public int MaxHitPoints { get; set; }
        
        /// <summary>Current action points available for playing cards</summary>
        public int ActionPoints { get; set; }
        
        /// <summary>Cards currently in the player's hand</summary>
        public List<Card> Hand { get; }
        
        /// <summary>Status effects currently affecting the player</summary>
        public List<StatusEffect> ActiveEffects { get; }

        /// <summary>
        /// Initializes a new player state with starting HP and empty hand
        /// </summary>
        public PlayerFightState(int startingHp)
        {
            HitPoints = startingHp;
            MaxHitPoints = startingHp;
            ActionPoints = 0;
            Hand = new List<Card>();
            ActiveEffects = new List<StatusEffect>();
        }
        
        /// <summary>
        /// Applies a status effect to the player
        /// </summary>
        public void ApplyStatusEffect(StatusEffect effect)
        {
            // Check if this effect already exists
            var existingEffect = ActiveEffects.FirstOrDefault(e => e.Id == effect.Id);
            if (existingEffect != null)
            {
                // Remove the existing effect
                ActiveEffects.Remove(existingEffect);
            }
            
            // Add the new effect
            ActiveEffects.Add(effect);
        }
        
        /// <summary>
        /// Processes all active status effects at the start of a turn
        /// </summary>
        public void ProcessStatusEffects(FightState state, string playerId)
        {
            // Apply each effect
            foreach (var effect in ActiveEffects.ToList())
            {
                effect.Apply(state, playerId);
                effect.Tick();
                
                // Remove expired effects
                if (effect.IsExpired)
                {
                    ActiveEffects.Remove(effect);
                }
            }
        }
    }
}
