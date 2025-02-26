using Proto;
using GameServer.Application.Models;
using GameServer.Application.Messages.Internal;
using GameServer.Shared.ExternalMessages;

namespace GameServer.Application.Actors
{
    public class MapActor : IActor
    {
        private readonly Map map;
        private readonly Dictionary<PID, string> players;
        private readonly Dictionary<string, PID> activeFights;
        private int fightCounter;

        public MapActor(string id, string name, int width, int height, int[] tileData)
        {
            map = new Map(id, name, width, height, tileData);
            players = new Dictionary<PID, string>();
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
                FightChallengeRequest msg => OnChallengeFightRequest(context, msg),
                FightChallengeResponse msg => OnFightChallengeResponse(context, msg),
                FightStarted msg => OnFightStarted(context, msg),
                FightCompleted msg => OnFightCompleted(context, msg),
                BroadcastExternalMessage msg => OnBroadcastExternalMessage(context, msg),
                _ => Task.CompletedTask
            };
        }

        private Task OnStarted(IContext context) => Task.CompletedTask;

        private Task OnAddPlayer(IContext context, AddPlayer msg)
        {
            // Get playerId from actor name
            var playerId = msg.PlayerActor.Id;
            
            if (map.TryAddPlayer(playerId, out var startPosition))
            {
                players.Add(msg.PlayerActor, playerId);
                BroadcastToAllPlayers(context, new OutPlayerJoinedMap(playerId, startPosition));
                var tilemapData = new GameServer.Shared.ExternalMessages.TilemapData(map.Width, map.Height, map.TileData);
                
                // Convert player positions to use PIDs
                var pidPositions = map.PlayerPositions.ToDictionary(
                    kvp => players.First(p => p.Value == kvp.Key).Key,
                    kvp => kvp.Value
                );
                
                context.Send(msg.Requester, new PlayerAddedToMap(msg.PlayerActor, context.Self, map.Id, startPosition, tilemapData, pidPositions));
            }
            else
            {
                context.Send(msg.Requester, new PlayerAddFailure(msg.PlayerActor, this.map.Id, "Map is full"));
            }
            return Task.CompletedTask;
        }

        private Task OnRemovePlayer(IContext context, RemovePlayer msg)
        {
            var playerId = players.GetValueOrDefault(msg.PlayerActor);
            if (playerId != null && map.RemovePlayer(playerId))
            {
                players.Remove(msg.PlayerActor);
                
                // Handle player disconnection if they're in a fight
                var fightId = map.GetPlayerFightId(playerId);
                if (fightId != null && activeFights.TryGetValue(fightId, out var fightActor))
                {
                    context.Send(fightActor, new PlayerDisconnected(msg.PlayerActor));
                }

                BroadcastToAllPlayers(context, new OutPlayerLeftMap(playerId));
                context.Send(msg.Requester, new PlayerRemovedFromMap(this.map.Id, msg.PlayerActor));
            }
            else
            {
                context.Send(msg.Requester, new PlayerRemoveFailure(this.map.Id, msg.PlayerActor, "Player not found in map"));
            }
            return Task.CompletedTask;
        }

        private Task OnValidateMove(IContext context, ValidateMove msg)
        {
            var playerId = players.GetValueOrDefault(msg.PlayerActor);
            if (playerId == null)
            {
                context.Send(msg.Requester, new MoveRejected(msg.PlayerActor, msg.NewPosition, "Player not found"));
                return Task.CompletedTask;
            }

            if (map.IsPlayerInFight(playerId))
            {
                context.Send(msg.Requester, new MoveRejected(msg.PlayerActor, msg.NewPosition, "Cannot move while in a fight"));
                return Task.CompletedTask;
            }

            if (map.TryMovePlayer(playerId, msg.NewPosition))
            {
                BroadcastToAllPlayers(context, new OutPlayerPositionChange(playerId, msg.NewPosition));
                context.Send(msg.Requester, new MoveValidated(msg.PlayerActor, msg.NewPosition));
            }
            else
            {
                context.Send(msg.Requester, new MoveRejected(msg.PlayerActor, msg.NewPosition, "Invalid move"));
            }
            return Task.CompletedTask;
        }

        private Task OnChallengeFightRequest(IContext context, FightChallengeRequest msg)
        {
            var challengerPlayerId = players.GetValueOrDefault(msg.ChallengerActor);
            var targetPlayerId = players.GetValueOrDefault(msg.TargetActor);

            if (challengerPlayerId == null || targetPlayerId == null)
            {
                context.Send(context.Sender, new FightChallengeResponse(msg.ChallengerActor, msg.Challenger, msg.TargetActor, null, false));
                return Task.CompletedTask;
            }

            var challengerPosition = map.GetPlayerPosition(challengerPlayerId);
            var targetPosition = map.GetPlayerPosition(targetPlayerId);

            if (challengerPosition == null || targetPosition == null)
            {
                context.Send(context.Sender, new FightChallengeResponse(msg.ChallengerActor, msg.Challenger, msg.TargetActor, null, false));
                return Task.CompletedTask;
            }

            if (map.IsPlayerInFight(challengerPlayerId) || map.IsPlayerInFight(targetPlayerId))
            {
                context.Send(context.Sender, new FightChallengeResponse(msg.ChallengerActor, msg.Challenger, msg.TargetActor, null, false));
                return Task.CompletedTask;
            }

            // Send challenge to target player
            context.Send(msg.TargetActor, msg);

            return Task.CompletedTask;
        }

        private Task OnFightChallengeResponse(IContext context, FightChallengeResponse msg)
        {
            if (!msg.Accepted) return Task.CompletedTask;

            var challengerPlayerId = players.GetValueOrDefault(msg.ChallengerActor);
            var targetPlayerId = players.GetValueOrDefault(msg.TargetActor);

            if (challengerPlayerId == null || targetPlayerId == null)
                return Task.CompletedTask;

            var challengerPosition = map.GetPlayerPosition(challengerPlayerId);
            var targetPosition = map.GetPlayerPosition(targetPlayerId);

            if (challengerPosition == null || targetPosition == null || 
                map.IsPlayerInFight(challengerPlayerId) || map.IsPlayerInFight(targetPlayerId))
            {
                return Task.CompletedTask;
            }

            // Create fight
            string fightId = $"fight_{++fightCounter}";
            var props = Props.FromProducer(() => 
                new FightActor(fightId, challengerPlayerId, msg.Challenger, targetPlayerId, msg.Target, context.Self));
            
            var fightActor = context.SpawnNamed(props, fightId);
            activeFights.Add(fightId, fightActor);

            // Update player states
            map.SetPlayerFightId(challengerPlayerId, fightId);
            map.SetPlayerFightId(targetPlayerId, fightId);

            return Task.CompletedTask;
        }

        private Task OnFightStarted(IContext context, FightStarted msg)
        {
            // Convert Player1Id/Player2Id from PlayerIds to PIDs
            var player1Actor = players[msg.Player1Actor];
            var player2Actor = players[msg.Player2Actor];

            if (player1Actor != null)
                context.Send(msg.Player1Actor, new OutFightStarted(msg.Player2.Id));
            if (player2Actor != null)
                context.Send(msg.Player2Actor, new OutFightStarted(msg.Player1.Id));

            return Task.CompletedTask;
        }

        private Task OnFightCompleted(IContext context, FightCompleted msg)
        {
            if (activeFights.Remove(msg.FightId, out var fightActor))
            {
                var winnerPlayerId = players.GetValueOrDefault(msg.WinnerActor);
                var loserPlayerId = players.GetValueOrDefault(msg.LoserActor);

                if (winnerPlayerId != null)
                    map.SetPlayerFightId(winnerPlayerId, null);
                if (loserPlayerId != null)
                    map.SetPlayerFightId(loserPlayerId, null);

                // Notify players of fight completion
                BroadcastToAllPlayers(context, new OutFightEnded(winnerPlayerId ?? "unknown", msg.Reason));
                
                // Stop the fight actor
                context.Stop(fightActor);
            }
            return Task.CompletedTask;
        }

        private async Task OnBroadcastExternalMessage(IContext context, BroadcastExternalMessage msg)
        {
            BroadcastToAllPlayers(context, msg.ExternalMessage);
        }

        private void BroadcastToAllPlayers(IContext context, object message)
        {
            foreach (var player in players)
            {
                context.Send(player.Key, message);
            }
        }
    }
}
