using Proto;
using GameServer.Application.Models;
using GameServer.Application.Messages.Internal;
using GameServer.Shared.ExternalMessages;

namespace GameServer.Application.Actors
{
    public class MapActor : IActor
    {
        private readonly Map map;
        private readonly HashSet<PID> players;
        private readonly Dictionary<string, PID> activeFights;
        private int fightCounter;

        public MapActor(string id, string name, int width, int height, int[] tileData)
        {
            map = new Map(id, name, width, height, tileData);
            players = new HashSet<PID>();
            activeFights = new Dictionary<string, PID>();
            fightCounter = 0;
        }

        public Task ReceiveAsync(IContext context)
        {
            return context.Message switch
            {
                Started => OnStarted(context),
                AddPlayer msg => OnAddPlayer(context, msg),
                RemovePlayer msg => OnRemovePlayer(context, msg),
                ValidateMove msg => OnValidateMove(context, msg),
                SubscribeToMapUpdates msg => OnSubscribeToMapUpdates(context, msg),
                UnsubscribeFromMapUpdates msg => OnUnsubscribeFromMapUpdates(context, msg),
                ChallengeFightRequest msg => OnChallengeFightRequest(context, msg),
                FightChallengeResponse msg => OnFightChallengeResponse(context, msg),
                FightStarted msg => OnFightStarted(context, msg),
                FightCompleted msg => OnFightCompleted(context, msg),
                _ => Task.CompletedTask
            };
        }

        private Task OnStarted(IContext context) => Task.CompletedTask;

        private Task OnAddPlayer(IContext context, AddPlayer msg)
        {
            if (map.TryAddPlayer(msg.PlayerId, out var startPosition))
            {
                players.Add(msg.PlayerActor);
                BroadcastToAllPlayers(context, new ExtPlayerJoinedMap(msg.PlayerId, startPosition));
                var tilemapData = new GameServer.Shared.ExternalMessages.TilemapData(map.Width, map.Height, map.TileData);
                context.Send(msg.Requester, new PlayerAddedToMap(msg.PlayerActor, context.Self, map.Id, startPosition, tilemapData));
            }
            else
            {
                context.Send(msg.Requester, new PlayerAddFailure(msg.PlayerActor, this.map.Id, "Map is full"));
            }
            return Task.CompletedTask;
        }

        private Task OnRemovePlayer(IContext context, RemovePlayer msg)
        {
            if (map.RemovePlayer(msg.PlayerId))
            {
                players.Remove(context.Sender);
                
                // Handle player disconnection if they're in a fight
                var fightId = map.GetPlayerFightId(msg.PlayerId);
                if (fightId != null && activeFights.TryGetValue(fightId, out var fightActor))
                {
                    context.Send(fightActor, new PlayerDisconnected(msg.PlayerId));
                }

                BroadcastToAllPlayers(context, new ExtPlayerLeftMap(msg.PlayerId));
                context.Send(msg.Requester, new PlayerRemovedFromMap(this.map.Id, msg.PlayerId));
            }
            else
            {
                context.Send(msg.Requester, new PlayerRemoveFailure(this.map.Id, msg.PlayerId, "Player not found in map"));
            }
            return Task.CompletedTask;
        }

        private Task OnValidateMove(IContext context, ValidateMove msg)
        {
            if (map.IsPlayerInFight(msg.PlayerId))
            {
                context.Send(msg.Requester, new MoveRejected(msg.PlayerId, msg.NewPosition, "Cannot move while in a fight"));
                return Task.CompletedTask;
            }

            if (map.TryMovePlayer(msg.PlayerId, msg.NewPosition))
            {
                BroadcastToAllPlayers(context, new ExtPlayerPositionChange(msg.PlayerId, msg.NewPosition));
                context.Send(msg.Requester, new MoveValidated(msg.PlayerId, msg.NewPosition));
            }
            else
            {
                context.Send(msg.Requester, new MoveRejected(msg.PlayerId, msg.NewPosition, "Invalid move"));
            }
            return Task.CompletedTask;
        }

        private Task OnChallengeFightRequest(IContext context, ChallengeFightRequest msg)
        {
            var challengerPosition = map.GetPlayerPosition(msg.ChallengerId);
            var targetPosition = map.GetPlayerPosition(msg.TargetId);

            if (challengerPosition == null || targetPosition == null)
            {
                context.Send(context.Sender, new FightChallengeResponse(msg.ChallengerId, msg.TargetId, false));
                return Task.CompletedTask;
            }

            if (map.IsPlayerInFight(msg.ChallengerId) || map.IsPlayerInFight(msg.TargetId))
            {
                context.Send(context.Sender, new FightChallengeResponse(msg.ChallengerId, msg.TargetId, false));
                return Task.CompletedTask;
            }

            // Send challenge to target player
            var targetActor = players.FirstOrDefault(p => map.GetPlayerPosition(msg.TargetId) != null);
            if (targetActor != null)
            {
                context.Send(targetActor, new ExtFightChallengeReceived(msg.ChallengerId));
            }

            return Task.CompletedTask;
        }

        private Task OnFightChallengeResponse(IContext context, FightChallengeResponse msg)
        {
            if (!msg.Accepted) return Task.CompletedTask;

            var challengerPosition = map.GetPlayerPosition(msg.ChallengerId);
            var targetPosition = map.GetPlayerPosition(msg.TargetId);

            if (challengerPosition == null || targetPosition == null || 
                map.IsPlayerInFight(msg.ChallengerId) || map.IsPlayerInFight(msg.TargetId))
            {
                return Task.CompletedTask;
            }

            // Create fight
            string fightId = $"fight_{++fightCounter}";
            var props = Props.FromProducer(() => 
                new FightActor(fightId, msg.ChallengerId, msg.TargetId, context.Self));
            
            var fightActor = context.SpawnNamed(props, fightId);
            activeFights.Add(fightId, fightActor);

            // Update player states
            map.SetPlayerFightId(msg.ChallengerId, fightId);
            map.SetPlayerFightId(msg.TargetId, fightId);

            return Task.CompletedTask;
        }

        private Task OnFightStarted(IContext context, FightStarted msg)
        {
            // Notify both players that the fight has started
            var player1Actor = players.FirstOrDefault(p => map.GetPlayerPosition(msg.Player1Id) != null);
            var player2Actor = players.FirstOrDefault(p => map.GetPlayerPosition(msg.Player2Id) != null);

            if (player1Actor != null)
                context.Send(player1Actor, new ExtFightStarted(msg.Player2Id));
            if (player2Actor != null)
                context.Send(player2Actor, new ExtFightStarted(msg.Player1Id));

            return Task.CompletedTask;
        }

        private Task OnFightCompleted(IContext context, FightCompleted msg)
        {
            if (activeFights.Remove(msg.FightId, out var fightActor))
            {
                map.SetPlayerFightId(msg.WinnerId, null);
                map.SetPlayerFightId(msg.LoserId, null);

                // Notify players of fight completion
                BroadcastToAllPlayers(context, new ExtFightEnded(msg.WinnerId, msg.Reason));
                
                // Stop the fight actor
                context.Stop(fightActor);
            }
            return Task.CompletedTask;
        }

        private Task OnSubscribeToMapUpdates(IContext context, SubscribeToMapUpdates msg)
        {
            players.Add(msg.Subscriber);
            context.Send(msg.Subscriber, new MapStateUpdate(
                map.Id,
                map.Name,
                map.Width,
                map.Height,
                map.TileData,
                map.PlayerPositions
            ));
            context.Send(msg.Subscriber, new MapUpdateSubscribed(msg.Subscriber));
            return Task.CompletedTask;
        }

        private Task OnUnsubscribeFromMapUpdates(IContext context, UnsubscribeFromMapUpdates msg)
        {
            players.Remove(msg.Subscriber);
            context.Send(msg.Subscriber, new MapUpdateUnsubscribed(msg.Subscriber));
            return Task.CompletedTask;
        }

        private void BroadcastToAllPlayers(IContext context, object message)
        {
            foreach (var subscriber in players)
            {
                context.Send(subscriber, message);
            }
        }
    }
}
