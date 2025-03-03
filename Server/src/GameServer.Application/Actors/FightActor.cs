using Proto;
using GameServer.Application.Models;
using GameServer.Application.Messages.Internal;
using GameServer.Shared.ExternalMessages;
using GameServer.Application.Models.CardEffects;

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
        private readonly ICardEffectHandlerFactory cardEffectHandlerFactory;
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
            
            // Initialize card effect handler factory
            var registry = new CardEffectHandlerRegistry(() => new DefaultCardEffectHandler());
            CardEffectHandlerRegistration.RegisterAllHandlers(registry);
            cardEffectHandlerFactory = registry;
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
            var playerState = state.GetPlayerState(playerId);
            
            // Get status effects before they're processed
            var activeEffects = new List<StatusEffect>(playerState.ActiveEffects);

            // Update fight state for new turn (this will process status effects)
            state.StartTurn(playerId);
            
            // Send notifications about status effects that were applied
            foreach (var effect in activeEffects)
            {
                // Only send notifications for effects that apply at turn start
                if (effect.Type == StatusEffectType.DamageOverTime || effect.Type == StatusEffectType.HealOverTime)
                {
                    string effectType = effect.Type == StatusEffectType.DamageOverTime ? "DamageOverTime" : "HealOverTime";
                    context.Send(mapActor, new OutEffectApplied(playerId, effectType, effect.Magnitude, effect.Source));
                }
            }

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

                // Send notifications to both players and the map actor
                var cardInfo = new CardInfo(card.Id, card.Name, card.Description, card.Cost);
                
                // Send to the map actor (for broadcasting to clients)
                context.Send(mapActor, new OutCardPlayCompleted(playerId, cardInfo, effect, true));
                
                // Get opponent actor
                PID opponentActor = msg.PlayerActor.Equals(player1Actor) ? player2Actor : player1Actor;
                
                // Send card SVG data to opponent so they can display the card
                if (!cardSvgCache.TryGetValue(card.Id, out var svgData))
                {
                    svgData = GetCardSvgData(card.Id);
                }
                
                // Send card images to opponent
                var cardSvgData = new Dictionary<string, string> { { card.Id, svgData } };
                context.Send(opponentActor, new OutCardImages(cardSvgData));
                
                // Send card play completed to opponent directly
                context.Send(opponentActor, new OutCardPlayCompleted(playerId, cardInfo, effect, true));

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

        /// <summary>
        /// Applies the effects of a card using the appropriate handler
        /// </summary>
        private string ApplyCardEffects(IContext context, PID playerActor, Card card)
        {
            string playerId = players[playerActor].Id;
            PID targetActor = playerActor.Equals(player1Actor) ? player2Actor : player1Actor;
            string targetId = players[targetActor].Id;
            
            // Get the appropriate handler for this card
            var handler = cardEffectHandlerFactory.CreateHandler(card);
            
            // Apply the effect and get the result
            var result = handler.ApplyEffect(context, state, playerId, targetId, card);
            
            // Forward notifications to the map actor
            foreach (var notification in result.Notifications)
            {
                context.Send(mapActor, notification);
            }
            
            return result.Description;
        }

        private void SendFightStateUpdate(IContext context)
        {
            var player1State = state.GetPlayerState(player1Actor.Id);
            var player2State = state.GetPlayerState(player2Actor.Id);

            // Convert status effects to client format
            var p1StatusEffects = player1State.ActiveEffects.Select(e => 
                new StatusEffectInfo(
                    e.Id,
                    e.Name,
                    e.Description,
                    e.Duration,
                    e.Type.ToString(),
                    e.Magnitude
                )
            ).ToList();
            
            var p2StatusEffects = player2State.ActiveEffects.Select(e => 
                new StatusEffectInfo(
                    e.Id,
                    e.Name,
                    e.Description,
                    e.Duration,
                    e.Type.ToString(),
                    e.Magnitude
                )
            ).ToList();

            var p1State = new OutPlayerFightState(
                players[player1Actor].Id,
                player1State.HitPoints,
                player1State.ActionPoints,
                player1State.Hand.Select(c => new CardInfo(c.Id, c.Name, c.Description, c.Cost)).ToList(),
                players[player1Actor].Deck.RemainingCards,
                players[player1Actor].Deck.DiscardPile.Count,
                p1StatusEffects
            );

            var p2State = new OutPlayerFightState(
                players[player2Actor].Id,
                player2State.HitPoints,
                player2State.ActionPoints,
                player2State.Hand.Select(c => new CardInfo(c.Id, c.Name, c.Description, c.Cost)).ToList(),
                players[player2Actor].Deck.RemainingCards,
                players[player2Actor].Deck.DiscardPile.Count,
                p2StatusEffects
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
