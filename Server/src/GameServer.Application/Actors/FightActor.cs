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
        private readonly string fightId;
        private readonly PID player1Actor;
        private readonly PID player2Actor;
        private readonly PID mapActor;
        private readonly FightState state;
        private readonly Dictionary<PID, Player> players;
        private bool isActive;

        public FightActor(string fightId, string player1Id, Player player1, string player2Id, Player player2, PID mapActor)
        {
            this.fightId = fightId;
            // Get PIDs from the actor system using connectionIds
            this.player1Actor = Proto.PID.FromAddress("nonhost", player1Id);
            this.player2Actor = Proto.PID.FromAddress("nonhost", player2Id);
            this.mapActor = mapActor;
            this.isActive = true;

            // Initialize fight state
            state = new FightState(player1Id, player2Id);
            players = new Dictionary<PID, Player>
            {
                { player1Actor, player1 },
                { player2Actor, player2 }
            };
        }

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

        private Task OnStarted(IContext context)
        {
            // Notify clients that fight has started
            context.Send(mapActor, new FightStarted(fightId, player1Actor, players[player1Actor], player2Actor, players[player2Actor]));

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

            // Convert to CardInfo for client
            var cardInfos = drawnCards.Select(c => new CardInfo(c.Id, c.Name, c.Description, c.Cost)).ToList();

            // Send turn started notification
            context.Send(msg.PlayerActor, new ExtTurnStarted(playerId, cardInfos));

            // Send updated fight state
            SendFightStateUpdate(context);

            return Task.CompletedTask;
        }

        private Task OnEndTurn(IContext context, EndTurn msg)
        {
            if (!isActive) return Task.CompletedTask;

            var playerId = players[msg.PlayerActor].Id;

            // Notify clients
            context.Send(mapActor, new ExtTurnEnded(playerId));

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
                    context.Send(mapActor, new ExtCardPlayFailed(msg.CardId, "Not enough action points"));
                    return Task.CompletedTask;
                }

                state.PlayCard(playerId, card);

                // Apply card effects
                string effect = ApplyCardEffects(context, msg.PlayerActor, card);

                // Send notifications
                var cardInfo = new CardInfo(card.Id, card.Name, card.Description, card.Cost);
                context.Send(mapActor, new ExtCardPlayCompleted(playerId, cardInfo, effect));

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
                context.Send(mapActor, new ExtCardPlayFailed(msg.CardId, ex.Message));
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
                    context.Send(mapActor, new ExtEffectApplied(targetId, "Damage", damage, card.Name));
                    break;

                case CardType.Defense:
                    int healing = GetHealingForCard(card);
                    state.ApplyHealing(players[playerActor].Id, healing);
                    effect = $"Healed for {healing}";
                    context.Send(mapActor, new ExtEffectApplied(players[playerActor].Id, "Heal", healing, card.Name));
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

            var p1State = new ExPlayerFightState(
                player1State.HitPoints,
                player1State.ActionPoints,
                player1State.Hand.Select(c => new CardInfo(c.Id, c.Name, c.Description, c.Cost)).ToList(),
                players[player1Actor].Deck.RemainingCards
            );

            var p2State = new ExPlayerFightState(
                player2State.HitPoints,
                player2State.ActionPoints,
                player2State.Hand.Select(c => new CardInfo(c.Id, c.Name, c.Description, c.Cost)).ToList(),
                players[player2Actor].Deck.RemainingCards
            );

            context.Send(mapActor, new ExtFightStateUpdate(
                state.CurrentTurnPlayerId,
                p1State,
                p2State
            ));
        }

        private Task OnPlayerDisconnected(IContext context, PlayerDisconnected msg)
        {
            if (!isActive) return Task.CompletedTask;

            PID winnerActor = msg.PlayerActor.Equals(player1Actor) ? player2Actor : player1Actor;

            isActive = false;
            context.Send(mapActor, new FightCompleted(fightId, winnerActor, msg.PlayerActor, "Player disconnected"));
            return Task.CompletedTask;
        }

        private Task OnEndFight(IContext context, EndFight msg)
        {
            if (!isActive) return Task.CompletedTask;

            isActive = false;
            context.Send(mapActor, new FightCompleted(fightId, msg.WinnerActor, msg.LoserActor, msg.Reason));
            return Task.CompletedTask;
        }
    }
}
