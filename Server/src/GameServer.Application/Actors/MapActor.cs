using Proto;
using GameServer.Application.Models;
using GameServer.Application.Messages.Internal;
using GameServer.Application.Extensions;
using GameServer.Shared;
using GameServer.Shared.Models;
using GameServer.Shared.Messages.CardBattle;
using GameServer.Shared.Messages.Map;
using GameServer.Shared.Messages.Movement;
using GameServer.Shared.Messages.Fight;

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
                JoinMap msg => OnAddPlayer(context, msg),
                LeaveMap msg => OnRemovePlayer(context, msg),
                TryMove msg => OnValidateMove(context, msg),
                FightChallengeRequest msg => OnChallengeFightRequest(context, msg),
                FightChallengeResponse msg => OnFightChallengeResponse(context, msg),
                FightCompleted msg => OnFightCompleted(context, msg),
                BroadcastExternalMessage msg => OnBroadcastExternalMessage(context, msg),
                ExtPlayCardRequest msg => OnCardMessage(context, msg),
                ExtEndTurnRequest msg => OnCardMessage(context, msg),
                _ => Task.CompletedTask
            };
        }

        private Task OnStarted(IContext context)
        {
            this.LogInformation("Map actor started for map: {0} ({1})", map.Name, map.Id);
            return Task.CompletedTask;
        }

        private Task OnAddPlayer(IContext context, JoinMap msg)
        {
            // Get playerId from actor name
            var playerId = msg.PlayerActor.Id;
            
            this.LogInformation("Player {0} attempting to join map {1}", playerId, map.Id);
            
            if (map.TryAddPlayer(playerId, out var startPosition))
            {
                this.LogInformation("Player {0} added to map at position {1},{2}", playerId, startPosition.X, startPosition.Y);
                players.Add(msg.PlayerActor, playerId);
                BroadcastToAllPlayers(context, new ExtPlayerJoinedMap(playerId, startPosition));
                var tilemapData = new TilemapData(map.Width, map.Height, map.TileData);
                
                // Create a dictionary of player info including positions and fight IDs
                var playerInfo = new Dictionary<string, PlayerMapInfo>();
                foreach (var kvp in map.PlayerPositions.Where(x => x.Key != playerId))
                {
                    // Get the player's fight ID if they're in a fight
                    var fightId = map.GetPlayerFightId(kvp.Key);
                    
                    // Add the player info to the dictionary with their ID as the key
                    playerInfo[kvp.Key] = new PlayerMapInfo(kvp.Value, fightId);
                }
                
                context.Send(msg.Requester, new PlayerAddedToMap(msg.PlayerActor, context.Self, map.Id, startPosition, tilemapData, playerInfo));
            }
            else
            {
                this.LogWarning("Failed to add player {0} to map: Map is full", playerId);
                context.Send(msg.Requester, new PlayerAddFailure(msg.PlayerActor, this.map.Id, "Map is full"));
            }
            return Task.CompletedTask;
        }

        private Task OnRemovePlayer(IContext context, LeaveMap msg)
        {
            var playerId = players.GetValueOrDefault(msg.PlayerActor);
            if (playerId != null)
            {
                this.LogInformation("Player {0} leaving map {1}", playerId, map.Id);
                
                // Handle player disconnection if they're in a fight
                var fightId = map.GetPlayerFightId(playerId);
                if (fightId != null && activeFights.TryGetValue(fightId, out var fightActor))
                {
                    this.LogInformation("Player {0} was in fight {1}, notifying fight actor", playerId, fightId);
                    context.Send(fightActor, new PlayerDisconnected(msg.PlayerActor));
                }
                map.RemovePlayer(playerId);
                players.Remove(msg.PlayerActor);

                BroadcastToAllPlayers(context, new ExtPlayerLeftMap(playerId));
                context.Send(msg.Requester, new PlayerRemovedFromMap(this.map.Id, msg.PlayerActor));
            }
            else
            {
                this.LogWarning("Failed to remove player from map: Player not found");
                context.Send(msg.Requester, new PlayerRemoveFailure(this.map.Id, msg.PlayerActor, "Player not found in map"));
            }
            return Task.CompletedTask;
        }

        private Task OnValidateMove(IContext context, TryMove msg)
        {
            var playerId = players.GetValueOrDefault(msg.PlayerActor);
            if (playerId == null)
            {
                this.LogWarning("Move validation failed: Player not found");
                context.Send(msg.Requester, new MoveRejected(msg.PlayerActor, msg.NewPosition, "Player not found"));
                return Task.CompletedTask;
            }

            if (map.IsPlayerInFight(playerId))
            {
                this.LogWarning("Move validation failed for player {0}: Cannot move while in a fight", playerId);
                context.Send(msg.Requester, new MoveRejected(msg.PlayerActor, msg.NewPosition, "Cannot move while in a fight"));
                return Task.CompletedTask;
            }

            this.LogDebug("Validating move for player {0} to position {1},{2}", 
                playerId, msg.NewPosition.X, msg.NewPosition.Y);
                
            if (map.TryMovePlayer(playerId, msg.NewPosition))
            {
                this.LogDebug("Move validated for player {0}", playerId);
                BroadcastToAllPlayers(context, new ExtPlayerPositionChange(playerId, msg.NewPosition));
                context.Send(msg.Requester, new MoveValidated(msg.PlayerActor, msg.NewPosition));
            }
            else
            {
                this.LogWarning("Move validation failed for player {0}: Invalid move", playerId);
                context.Send(msg.Requester, new MoveRejected(msg.PlayerActor, msg.NewPosition, "Invalid move"));
            }
            return Task.CompletedTask;
        }

        private Task OnChallengeFightRequest(IContext context, FightChallengeRequest msg)
        {
            var challengerPlayerId = players.GetValueOrDefault(msg.ChallengerActor);
            var targetPlayerId = players.GetValueOrDefault(msg.TargetActor);

            this.LogInformation("Fight challenge request from player {0} to player {1}", 
                challengerPlayerId, targetPlayerId);

            if (challengerPlayerId == null || targetPlayerId == null)
            {
                this.LogWarning("Fight challenge failed: One or both players not found");
                context.Send(msg.ChallengerActor, new FightChallengeResponse(msg.ChallengerActor, msg.Challenger, msg.TargetActor, null, false));
                return Task.CompletedTask;
            }

            var challengerPosition = map.GetPlayerPosition(challengerPlayerId);
            var targetPosition = map.GetPlayerPosition(targetPlayerId);

            if (challengerPosition == null || targetPosition == null)
            {
                this.LogWarning("Fight challenge failed: One or both players have no position");
                context.Send(msg.ChallengerActor, new FightChallengeResponse(msg.ChallengerActor, msg.Challenger, msg.TargetActor, null, false));
                return Task.CompletedTask;
            }

            if (map.IsPlayerInFight(challengerPlayerId) || map.IsPlayerInFight(targetPlayerId))
            {
                this.LogWarning("Fight challenge failed: One or both players already in a fight");
                context.Send(msg.ChallengerActor, new FightChallengeResponse(msg.ChallengerActor, msg.Challenger, msg.TargetActor, null, false));
                return Task.CompletedTask;
            }

            this.LogInformation("Forwarding fight challenge from {0} to {1}", challengerPlayerId, targetPlayerId);
            // Send challenge to target player
            context.Send(msg.TargetActor, msg);

            return Task.CompletedTask;
        }

        private Task OnFightChallengeResponse(IContext context, FightChallengeResponse msg)
        {
            var challengerPlayerId = players.GetValueOrDefault(msg.ChallengerActor);
            var targetPlayerId = players.GetValueOrDefault(msg.TargetActor);
            
            this.LogInformation("Fight challenge response: {0}", msg.Accepted ? "Accepted" : "Rejected");
            
            if (!msg.Accepted) 
            {
                this.LogInformation("Fight challenge rejected by player {0}", targetPlayerId);
                return Task.CompletedTask;
            }

            if (challengerPlayerId == null || targetPlayerId == null)
            {
                this.LogWarning("Cannot start fight: One or both players not found");
                return Task.CompletedTask;
            }

            var challengerPosition = map.GetPlayerPosition(challengerPlayerId);
            var targetPosition = map.GetPlayerPosition(targetPlayerId);

            if (challengerPosition == null || targetPosition == null || 
                map.IsPlayerInFight(challengerPlayerId) || map.IsPlayerInFight(targetPlayerId))
            {
                this.LogWarning("Cannot start fight: Invalid player state");
                return Task.CompletedTask;
            }

            // Create fight
            string fightId = $"fight_{++fightCounter}";
            this.LogInformation("Creating new fight {0} between players {1} and {2}", 
                fightId, challengerPlayerId, targetPlayerId);
                
            var props = Props.FromProducer(() => 
                new FightActor(msg.ChallengerActor, msg.Challenger, msg.TargetActor, msg.Target, context.Self));
            
            var fightActor = context.SpawnNamed(props, fightId);
            activeFights.Add(fightActor.Id, fightActor);

            // Update player states
            map.SetPlayerFightId(challengerPlayerId, fightActor.Id);
            map.SetPlayerFightId(targetPlayerId, fightActor.Id);
            
            // Broadcast to all players on the map that a fight has started
            this.LogInformation("Broadcasting fight start between players {0} and {1}", 
                challengerPlayerId, targetPlayerId);
            BroadcastToAllPlayers(context, new ExtFightStarted(fightActor.Id, challengerPlayerId, targetPlayerId));

            return Task.CompletedTask;
        }

        /// <summary>
        /// Handles the completion of a fight between players.
        /// Clears fight state for both players, allowing them to move again.
        /// Broadcasts the fight result to all players and stops the fight actor.
        /// </summary>
        private Task OnFightCompleted(IContext context, FightCompleted msg)
        {
            this.LogInformation("Fight {0} completed. Winner: {1}", msg.FightId.Id, msg.WinnerActor.Id);
            
            if (activeFights.Remove(msg.FightId.Id, out var fightActor))
            {
                var winnerPlayerId = players.GetValueOrDefault(msg.WinnerActor);
                var loserPlayerId = players.GetValueOrDefault(msg.LoserActor);

                // Clear fight IDs to allow movement
                if (winnerPlayerId != null)
                {
                    map.SetPlayerFightId(winnerPlayerId, null);
                    this.LogInformation("Player {0} can now move after fight", winnerPlayerId);
                }
                    
                if (loserPlayerId != null)
                {
                    map.SetPlayerFightId(loserPlayerId, null);
                    this.LogInformation("Player {0} can now move after fight", loserPlayerId);
                }

                // Notify players of fight completion
                BroadcastToAllPlayers(context, new ExtFightEnded(
                    msg.FightId.Id,
                    winnerPlayerId ?? "unknown", 
                    loserPlayerId ?? "unknown", 
                    msg.Reason));
                
                // Stop the fight actor
                context.Stop(fightActor);
            }
            else
            {
                this.LogWarning("Fight completion for unknown fight: {0}", msg.FightId.Id);
            }
            return Task.CompletedTask;
        }

        private async Task OnBroadcastExternalMessage(IContext context, BroadcastExternalMessage msg)
        {
            this.LogDebug("Broadcasting message to all players: {0}", JsonConfig.Serialize(msg.ExternalMessage));
            BroadcastToAllPlayers(context, msg.ExternalMessage);
        }

        private Task OnCardMessage(IContext context, object msg)
        {
            // Get the player ID from the sender
            if (!players.TryGetValue(context.Sender, out var playerId))
            {
                this.LogWarning("Card message from unknown player");
                return Task.CompletedTask;
            }

            // Get the fight ID for the player
            var fightId = map.GetPlayerFightId(playerId);
            if (fightId == null || !activeFights.TryGetValue(fightId, out var fightActor))
            {
                this.LogWarning("Card message from player {0} who is not in a fight", playerId);
                return Task.CompletedTask;
            }

            this.LogDebug("Forwarding card message from player {0} to fight {1}", playerId, fightId);
            // Forward the message to the fight actor
            context.Send(fightActor, msg);
            return Task.CompletedTask;
        }

        private void BroadcastToAllPlayers(IContext context, object message)
        {
            this.LogDebug("Broadcasting to {0} players", players.Count);
            foreach (var player in players)
            {
                context.Send(player.Key, message);
            }
        }
    }
}
