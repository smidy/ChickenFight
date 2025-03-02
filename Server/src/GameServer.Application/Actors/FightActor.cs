using Proto;
using GameServer.Application.Models;
using GameServer.Application.Messages.Internal;
using GameServer.Shared.ExternalMessages;

namespace GameServer.Application.Actors
{
    /// <summary>
    /// This actor represents a turn-based card battle between two players.
    /// Players start with 50 HP and 3 action points per turn.
    /// Players draw 5 cards per turn up to a maximum hand size of 10.
    /// Unused action points carry over to the next turn.
    /// The fight ends when one player loses all their HP.
    /// </summary>
    public class FightActor : IActor
    {
        private readonly PID player1Actor;
        private readonly PID player2Actor;
        private readonly PID mapActor;
        private readonly FightState state;
        private readonly Dictionary<PID, Player> players;
        private bool isActive;

        public FightActor(PID player1Actor, Player player1, PID player2Actor, Player player2, PID mapActor)
        {
            // Get PIDs from the actor system using connectionIds
            this.player1Actor = player1Actor;
            this.player2Actor = player2Actor;
            this.mapActor = mapActor;
            this.isActive = true;

            // Initialize fight state
            state = new FightState(player1Actor.Id, player2Actor.Id);
            players = new Dictionary<PID, Player>
            {
                { this.player1Actor, player1 },
                { this.player2Actor, player2 }
            };
        }
        
        // Cache for card SVG data
        private readonly Dictionary<string, string> cardSvgCache = new Dictionary<string, string>();

        public Task ReceiveAsync(IContext context)
        {
            return context.Message switch
            {
                Started => OnStarted(context),
                PlayerDisconnected msg => OnPlayerDisconnected(context, msg),
                EndFight msg => OnEndFight(context, msg),
                StartTurn msg => OnStartTurn(context, msg),
                EndTurn msg => OnEndTurn(context, msg),
                PlayCard msg => OnPlayCard(context, msg),
                _ => Task.CompletedTask
            };
        }
        
        /// <summary>
        /// Reads the SVG data for a card from the assets directory
        /// </summary>
        private string GetCardSvgData(string cardId)
        {
            // Check if we already have this card in the cache
            if (cardSvgCache.TryGetValue(cardId, out var svgData))
            {
                return svgData;
            }
            
            // Read the SVG data from the file
            var filePath = $"Assets\\{cardId}.txt";
            if (!File.Exists(filePath))
            {
                // If the specific card file doesn't exist, use the template
                filePath = "Assets\\card_template.txt";
            }
            
            svgData = File.ReadAllText(filePath);
            
            // Cache the SVG data
            cardSvgCache[cardId] = svgData;
            
            return svgData;
        }
        
        /// <summary>
        /// Collects SVG data for all cards in a player's hand
        /// </summary>
        private Dictionary<string, string> GetCardSvgsForHand(List<Card> hand)
        {
            var svgData = new Dictionary<string, string>();
            
            foreach (var card in hand)
            {
                svgData[card.Id] = GetCardSvgData(card.Id);
            }
            
            return svgData;
        }

        private Task OnStarted(IContext context)
        {
            context.Send(player1Actor, new FightStarted(context.Self, player1Actor, players[player1Actor], player2Actor, players[player2Actor]));
            context.Send(player2Actor, new FightStarted(context.Self, player1Actor, players[player1Actor], player2Actor, players[player2Actor]));

            // Start first turn
            return OnStartTurn(context, new StartTurn(player1Actor));
        }

        private Task OnStartTurn(IContext context, StartTurn msg)
        {
            if (!isActive) return Task.CompletedTask;

            var playerId = msg.PlayerActor.Id;

            // Update fight state for new turn
            state.StartTurn(playerId);

            // Draw cards for active player
            var player = players[msg.PlayerActor];
            var drawnCards = state.DrawCards(playerId, player.Deck);

            // Send turn started notification (without drawn cards)
            context.Send(msg.PlayerActor, new OutTurnStarted(playerId));
            
            // Send card SVG data for the drawn cards
            var cardSvgData = new Dictionary<string, string>();
            foreach (var card in drawnCards)
            {
                cardSvgData[card.Id] = GetCardSvgData(card.Id);
                
                // Also send individual card drawn messages with SVG data
                var cardInfo = new CardInfo(card.Id, card.Name, card.Description, card.Cost);
                context.Send(msg.PlayerActor, new OutCardDrawn(cardInfo, GetCardSvgData(card.Id)));
            }
            
            // Send all card SVGs in one message
            context.Send(msg.PlayerActor, new OutCardImages(cardSvgData));

            // Send updated fight state (which now has the sole responsibility for informing about hand contents)
            SendFightStateUpdate(context);

            return Task.CompletedTask;
        }

        private Task OnEndTurn(IContext context, EndTurn msg)
        {
            if (!isActive) return Task.CompletedTask;

            var playerId = players[msg.PlayerActor].Id;
            
            // Discard the player's hand at the end of their turn
            state.DiscardHand(playerId, players[msg.PlayerActor].Deck);

            // Notify clients
            context.Send(msg.PlayerActor, new OutTurnEnded(playerId));

            // Start next player's turn
            PID nextPlayerActor = msg.PlayerActor.Equals(player1Actor) ? player2Actor : player1Actor;
            return OnStartTurn(context, new StartTurn(nextPlayerActor));
        }        

        private Task OnPlayCard(IContext context, PlayCard msg)
        {
            if (!isActive) return Task.CompletedTask;

            var playerId = players[msg.PlayerActor].Id;
            if (!state.IsPlayersTurn(playerId)) return Task.CompletedTask;

            try
            {
                // Get the card from player's hand
                var playerState = state.GetPlayerState(playerId);
                var card = playerState.Hand.FirstOrDefault(c => c.Id == msg.CardId);
                if (card == null) return Task.CompletedTask;

                // Validate and play the card
                if (!state.CanPlayCard(playerId, card))
                {
                    context.Send(mapActor, new OutCardPlayFailed(msg.CardId, "Not enough action points"));
                    return Task.CompletedTask;
                }

                state.PlayCard(playerId, card);

                // Apply card effects
                string effect = ApplyCardEffects(context, msg.PlayerActor, card);

                // Send notifications
                var cardInfo = new CardInfo(card.Id, card.Name, card.Description, card.Cost);
                context.Send(mapActor, new OutCardPlayCompleted(playerId, cardInfo, effect));

                // Check for game over
                if (state.IsGameOver)
                {
                    string winnerPlayerId = state.GetWinnerId();
                    PID winnerActor = winnerPlayerId == players[player1Actor].Id ? player1Actor : player2Actor;
                    PID loserActor = winnerActor.Equals(player1Actor) ? player2Actor : player1Actor;
                    return OnEndFight(context, new EndFight(winnerActor, loserActor, "Player defeated"));
                }

                // Update fight state
                SendFightStateUpdate(context);
            }
            catch (Exception ex)
            {
                context.Send(mapActor, new OutCardPlayFailed(msg.CardId, ex.Message));
            }

            return Task.CompletedTask;
        }

        private string ApplyCardEffects(IContext context, PID playerActor, Card card)
        {
            PID targetActor = playerActor.Equals(player1Actor) ? player2Actor : player1Actor;
            string targetId = players[targetActor].Id;
            string effect = "";

            switch (card.Type)
            {
                case CardType.Attack:
                    int damage = GetDamageForCard(card);
                    state.ApplyDamage(targetId, damage);
                    effect = $"Dealt {damage} damage";
                    context.Send(mapActor, new OutEffectApplied(targetId, "Damage", damage, card.Name));
                    break;

                case CardType.Defense:
                    int healing = GetHealingForCard(card);
                    state.ApplyHealing(players[playerActor].Id, healing);
                    effect = $"Healed for {healing}";
                    context.Send(mapActor, new OutEffectApplied(players[playerActor].Id, "Heal", healing, card.Name));
                    break;

                    // Add other card type effects as needed
            }

            return effect;
        }

        private int GetDamageForCard(Card card)
        {
            // Basic damage values based on card cost
            return card.Cost * 2;
        }

        private int GetHealingForCard(Card card)
        {
            // Basic healing values based on card cost
            return card.Cost * 2;
        }

        private void SendFightStateUpdate(IContext context)
        {
            var player1State = state.GetPlayerState(player1Actor.Id);
            var player2State = state.GetPlayerState(player2Actor.Id);

            var p1State = new OutPlayerFightState(
                players[player1Actor].Id,  // Add player ID
                player1State.HitPoints,
                player1State.ActionPoints,
                player1State.Hand.Select(c => new CardInfo(c.Id, c.Name, c.Description, c.Cost)).ToList(),
                players[player1Actor].Deck.RemainingCards,
                players[player1Actor].Deck.DiscardPile.Count  // Add discard pile count
            );

            var p2State = new OutPlayerFightState(
                players[player2Actor].Id,  // Add player ID
                player2State.HitPoints,
                player2State.ActionPoints,
                player2State.Hand.Select(c => new CardInfo(c.Id, c.Name, c.Description, c.Cost)).ToList(),
                players[player2Actor].Deck.RemainingCards,
                players[player2Actor].Deck.DiscardPile.Count  // Add discard pile count
            );

            var outFightStateUpdate = new OutFightStateUpdate(
                state.CurrentTurnPlayerId,
                p1State,
                p2State
            );

            context.Send(player1Actor, outFightStateUpdate);
            context.Send(player2Actor, outFightStateUpdate);
        }

        /// <summary>
        /// Handles player disconnection during a fight.
        /// Ends the fight and notifies all relevant actors.
        /// </summary>
        private Task OnPlayerDisconnected(IContext context, PlayerDisconnected msg)
        {
            if (!isActive) return Task.CompletedTask;

            PID winnerActor = msg.PlayerActor.Equals(player1Actor) ? player2Actor : player1Actor;

            isActive = false;
            var fightCompletedMessage = new FightCompleted(context.Self, winnerActor, msg.PlayerActor, "Player disconnected");
            
            // Send fight completed message to both players and the map
            context.Send(player1Actor, fightCompletedMessage);
            context.Send(player2Actor, fightCompletedMessage);
            context.Send(mapActor, fightCompletedMessage);  // Also notify the map actor
            
            return Task.CompletedTask;
        }

        /// <summary>
        /// Handles the end of a fight.
        /// Notifies all relevant actors about the fight outcome.
        /// </summary>
        private Task OnEndFight(IContext context, EndFight msg)
        {
            if (!isActive) return Task.CompletedTask;

            isActive = false;
            var fightCompletedMessage = new FightCompleted(context.Self, msg.WinnerActor, msg.LoserActor, msg.Reason);
            
            // Send fight completed message to both players and the map
            context.Send(player1Actor, fightCompletedMessage);
            context.Send(player2Actor, fightCompletedMessage);
            context.Send(mapActor, fightCompletedMessage);  // Also notify the map actor
            
            return Task.CompletedTask;
        }
    }
}
