using Proto;
using GameServer.Application.Models;
using GameServer.Application.Messages.Internal;
using GameServer.Shared.Models;
using GameServer.Shared.Messages.Base;
using GameServer.Shared.Messages.Connection;
using GameServer.Shared.Messages.Map;
using GameServer.Shared.Messages.Movement;
using GameServer.Shared.Messages.Fight;
using GameServer.Shared.Messages.CardBattle;
using GameServer.Shared.Messages.State;
using GameServer.Application.Extensions;
using GameServer.Shared;

namespace GameServer.Application.Actors
{
    public delegate Task SendToClientDelegate<in T>(T message) where T : ExtServerMessage;

    /// <summary>
    /// This actor represents a player in the game. 
    /// It recieves messages from the client and other Actors in the Actor system.
    /// It sends 'ToClientMessage' messages to the players client using the delegate method _sendToClient
    /// </summary>
    public class PlayerActor : IActor
    {
        private readonly Player player;
        private readonly PID mapManagerActor;
        private readonly SendToClientDelegate<ExtServerMessage> _sendToClient;
        private PID? currentMap;
        private PID? currentFight;
        private string? pendingMapJoin;
        private MapPosition? pendingMove;

        public PlayerActor(PID mapManagerActor, string playerName, SendToClientDelegate<ExtServerMessage> sendToClient)
        {
            player = new Player(playerName);
            this.mapManagerActor = mapManagerActor;
            _sendToClient = sendToClient;
        }

        public Task ReceiveAsync(IContext context)
        {
            return context.Message switch
            {
                Started => OnStarted(context),
                MapListResponse msg => OnMapListResponse(context, msg),
                               
                // Map join messages
                PlayerAddedToMap msg => OnPlayerAddedToMap(context, msg),
                PlayerAddFailure msg => OnPlayerAddFailure(context, msg),
                
                // Map leave messages
                PlayerRemovedFromMap msg => OnPlayerRemovedFromMap(context, msg),
                PlayerRemoveFailure msg => OnPlayerRemoveFailure(context, msg),
                
                // Movement messages
                MoveValidated msg => OnMoveValidated(context, msg),
                MoveRejected msg => OnMoveRejected(context, msg),
                
                // Map state messages
                MapStateUpdate msg => OnMapStateUpdate(context, msg),

                // Fight messages

                FightChallengeRequest msg => OnFightChallengeRequestReceived(context, msg),
                FightStarted msg => OnFightStarted(context, msg),
                FightCompleted msg => OnFightCompleted(context, msg),

                ExtClientMessage msg => OnIncommingClientMessage(context, msg),

                //Send external messages to client
                ExtServerMessage msg => OnOutgoingClientMessage(context, msg),

                _ => Task.CompletedTask
            };
        }

        private async Task OnStarted(IContext context) {
            //Set the Player.Id as the Actor name/Id
            this.player.SetId(context.Self.Id);
            this.LogInformation("Player actor started with ID: {0}", this.player.Id);
        }

        private PID? GetMapPid(IContext context, string actorName)
        {
            return context.System.ProcessRegistry.Find(x => x == $"$1/MapManager/{actorName}").FirstOrDefault();
        }

        private async Task OnPlayerAddedToMap(IContext context, PlayerAddedToMap msg)
        {
            if (pendingMapJoin != null && msg.PlayerActor.Equals(context.Self))
            {
                this.LogInformation("Player added to map: {0} at position {1},{2}", msg.MapId, msg.StartPosition.X, msg.StartPosition.Y);
                currentMap = msg.MapPID;
                player.JoinMap(pendingMapJoin, msg.StartPosition);
                
                // Pass the player info directly to the client
                await _sendToClient(new ExtJoinMapCompleted(msg.MapId, player.Id, msg.StartPosition, msg.TilemapData, msg.PlayerInfo));
                pendingMapJoin = null;
            }
        }

        private async Task OnPlayerAddFailure(IContext context, PlayerAddFailure msg)
        {
            if (pendingMapJoin != null && msg.PlayerActor.Equals(context.Self))
            {
                this.LogWarning("Failed to add player to map {0}: {1}", msg.MapId, msg.Error);
                await _sendToClient(new ExtJoinMapFailed(msg.MapId, msg.Error));
                pendingMapJoin = null;
            }
        }
        /// <summary>
        /// Handles the player's request to challenge another player to a fight.
        /// Finds the target player and sends a challenge request to the map.
        /// </summary>
        private async Task OnFightChallengeSend(IContext context, ExtFightChallengeRequest msg)
        {
            if (currentMap == null)
            {
                this.LogWarning("Cannot challenge player to fight: not in a map");
                return;
            }

            if (msg.TargetId == null)
            {
                this.LogWarning("Cannot challenge player to fight: invalid TargetId");
                return;
            }

            this.LogInformation("Player challenging {0} to a fight", msg.TargetId);
            // Find target PID using the actor system
            var targetPid = context.System.ProcessRegistry.Find(x => x == msg.TargetId).FirstOrDefault();
            if (targetPid != null)
            {
                context.Send(currentMap, new FightChallengeRequest(context.Self, player, targetPid));
            }
            else
            {
                this.LogWarning("Target player {0} not found for fight challenge", msg.TargetId);
            }
        }

        /// <summary>
        /// Handles a fight challenge request received from another player.
        /// Currently auto-accepts all challenges, but could be modified to wait for player input.
        /// </summary>
        private async Task OnFightChallengeRequestReceived(IContext context, FightChallengeRequest msg)
        {
            if (currentMap == null)
            {
                this.LogWarning("Cannot accept fight challenge: not in a map");
                return;
            }

            this.LogInformation("Received fight challenge from {0}", msg.Challenger.Id);
            // Auto-accept for now - in a real implementation, you'd wait for player input
            context.Send(currentMap, new FightChallengeResponse(msg.ChallengerActor, msg.Challenger, context.Self, player, true));
        }

        /// <summary>
        /// Handles the FightStarted message received when a fight begins.
        /// Updates the player's fight state and notifies the client.
        /// </summary>
        private async Task OnFightStarted(IContext context, FightStarted msg)
        {
            this.LogInformation("Fight started with opponent {0}", msg.Player1.Id == player.Id ? msg.Player2.Id : msg.Player1.Id);
            currentFight = msg.FightActor;
            // Update player's fight state
            player.EnterFight(msg.FightActor.Id);
        }

        /// <summary>
        /// Handles the FightCompleted message received when a fight ends.
        /// Resets the player's fight state and notifies the client about the fight outcome.
        /// </summary>
        private async Task OnFightCompleted(IContext context, FightCompleted msg)
        {
            // Reset fight state
            currentFight = null;
            player.LeaveFight();
                        
            this.LogInformation("Fight completed. Winner: {0}. Reason: {1}", msg.WinnerActor.Id, msg.Reason);
        }

        private async Task OnPlayerRemovedFromMap(IContext context, PlayerRemovedFromMap msg)
        {
            if (msg.PlayerActor.Equals(context.Self))
            {
                this.LogInformation("Player removed from map: {0}", msg.MapId);
                player.LeaveMap();
                currentMap = null;
                await _sendToClient(new ExtLeaveMapCompleted(msg.MapId));
            }
        }

        private async Task OnPlayerRemoveFailure(IContext context, PlayerRemoveFailure msg)
        {
            if (msg.PlayerActor.Equals(context.Self))
            {
                this.LogWarning("Failed to remove player from map {0}: {1}", msg.MapId, msg.Error);
                await _sendToClient(new ExtLeaveMapFailed(msg.MapId, msg.Error));
            }
        }

        private async Task OnMoveValidated(IContext context, MoveValidated msg)
        {
            if (msg.PlayerActor.Equals(context.Self) && 
                pendingMove?.X == msg.NewPosition.X && 
                pendingMove?.Y == msg.NewPosition.Y)
            {
                this.LogDebug("Player move validated to position {0},{1}", msg.NewPosition.X, msg.NewPosition.Y);
                player.UpdatePosition(msg.NewPosition);
                await _sendToClient(new ExtPlayerInfo(new PlayerState(player.Id, player.Name, player.Position)));
                await _sendToClient(new ExtMoveCompleted(msg.NewPosition));
                pendingMove = null;
            }
        }

        private async Task OnMoveRejected(IContext context, MoveRejected msg)
        {
            if (msg.PlayerActor.Equals(context.Self) && 
                pendingMove?.X == msg.AttemptedPosition.X && 
                pendingMove?.Y == msg.AttemptedPosition.Y)
            {
                this.LogWarning("Player move rejected to position {0},{1}: {2}", 
                    msg.AttemptedPosition.X, msg.AttemptedPosition.Y, msg.Error);
                await _sendToClient(new ExtMoveFailed(msg.AttemptedPosition, msg.Error));
                pendingMove = null;
            }
        }

        private async Task OnMapListResponse(IContext context, MapListResponse msg)
        {
            this.LogDebug("Received map list with {0} maps", msg.Maps.Count);
            var mapInfos = msg.Maps.Select(m => new MapInfo(
                m.Id,
                m.Name,
                m.Width,
                m.Height,
                m.PlayerPositions.Count
            ));
            await _sendToClient(new ExtMapListResponse(mapInfos.ToList()));
        }

        private async Task OnMapStateUpdate(IContext context, MapStateUpdate msg)
        {
            this.LogDebug("Received map state update");
        }

        /// <summary>
        /// Handles the player's request to play a card during a fight.
        /// Forwards the request to the current fight actor if in a fight.
        /// </summary>
        private async Task OnPlayCard(IContext context, ExtPlayCardRequest msg)
        {
            if (currentFight != null)
            {
                this.LogInformation("Player playing card: {0}", msg.CardId);
                context.Send(currentFight, new PlayCard(context.Self, msg.CardId));
            }
            else
            {
                this.LogWarning("Cannot play card: not in a fight");
            }
        }

        /// <summary>
        /// Handles the player's request to end their turn during a fight.
        /// Forwards the request to the current fight actor if in a fight.
        /// </summary>
        private async Task OnEndTurn(IContext context, ExtEndTurnRequest msg)
        {
            if (currentFight != null)
            {
                this.LogInformation("Player ending turn");
                context.Send(currentFight, new EndTurn(context.Self));
            }
            else
            {
                this.LogWarning("Cannot end turn: not in a fight");
            }
        }

        private async Task OnRequestPlayerId(IContext context, ExtPlayerIdRequest msg)
        {
            this.LogInformation("Player requested PlayerId");
            await _sendToClient(new ExtPlayerIdResponse(player.Id));
        }

        private async Task OnIncommingClientMessage(IContext context, ExtClientMessage extClientMessage)
        {
            // Log incoming message at debug level with JSON
            this.LogDebug("Received client message: {0}", JsonConfig.Serialize(extClientMessage));
            
            var handle = extClientMessage switch
            {
                ExtPlayerIdRequest msg => OnRequestPlayerId(context, msg),
                ExtMapListRequest msg => OnRequestMapList(context, msg),
                ExtJoinMapRequest msg => OnJoinMap(context, msg),
                ExtLeaveMapRequest msg => OnLeaveMap(context, msg),
                ExtPlayerMoveRequest msg => OnPlayerMove(context, msg),
                ExtFightChallengeRequest msg => OnFightChallengeSend(context, msg),
                ExtPlayCardRequest msg => OnPlayCard(context, msg),
                ExtEndTurnRequest msg => OnEndTurn(context, msg),
                _ => Task.CompletedTask
            };
            await handle;
        }

        private async Task OnOutgoingClientMessage(IContext context, ExtServerMessage extServerMessage)
        {
            // Log outgoing message at debug level with JSON
            this.LogDebug("Sending client message: {0}", JsonConfig.Serialize(extServerMessage));
            await _sendToClient(extServerMessage);
        }

        private async Task OnRequestMapList(IContext context, ExtMapListRequest extMapListRequest)
        {
            this.LogInformation("Player requested map list");
            context.Send(mapManagerActor, new RequestMapList(context.Self));
        }

        private async Task OnJoinMap(IContext context, ExtJoinMapRequest extJoinMapRequest)
        {
            this.LogInformation("Player attempting to join map: {0}", extJoinMapRequest.MapId);
            
            if (currentMap != null)
            {
                this.LogWarning("Join map failed: Player already in map {0}", player.CurrentMapId);
                await _sendToClient(new ExtJoinMapFailed(extJoinMapRequest.MapId, "Already in a map"));
            }

            pendingMapJoin = extJoinMapRequest.MapId;

            var foundMap = GetMapPid(context, extJoinMapRequest.MapId);

            if (foundMap == null)
            {
                this.LogWarning("Join map failed: Invalid map ID {0}", extJoinMapRequest.MapId);
                await _sendToClient(new ExtJoinMapFailed(extJoinMapRequest.MapId, "Invalid Map Id"));
                return;
            }

            this.LogInformation("Sending join request to map {0}", extJoinMapRequest.MapId);
            context.Send(foundMap, new JoinMap(context.Self, player.Name, context.Self));
            await _sendToClient(new ExtJoinMapInitiated(extJoinMapRequest.MapId));
        }

        private async Task OnLeaveMap(IContext context, ExtLeaveMapRequest extLeaveMapRequest)
        {
            this.LogInformation("Player attempting to leave map: {0}", extLeaveMapRequest.MapId ?? "current map");
            
            if (currentMap == null)
            {
                this.LogWarning("Leave map failed: Player not in any map");
                await _sendToClient(new ExtLeaveMapFailed(extLeaveMapRequest.MapId, "Player has not joined a map"));
                return;
            }
            if (extLeaveMapRequest.MapId != null && extLeaveMapRequest.MapId != player.CurrentMapId)
            {
                this.LogWarning("Leave map failed: Invalid map ID {0}, player is in {1}", extLeaveMapRequest.MapId, player.CurrentMapId);
                await _sendToClient(new ExtLeaveMapFailed(extLeaveMapRequest.MapId, "Invalid map id specified"));
                return;
            }
            if (player.IsInFight && extLeaveMapRequest.MapId != null)
            {
                this.LogWarning("Leave map failed: Player is in a fight");
                await _sendToClient(new ExtLeaveMapFailed(extLeaveMapRequest.MapId, "Cannot leave map while in a fight"));
                return;
            }

            this.LogInformation("Sending leave request for map {0}", player.CurrentMapId);
            context.Send(currentMap, new LeaveMap(context.Self, context.Self));
            await _sendToClient(new ExtLeaveMapInitiated(extLeaveMapRequest.MapId));
        }

        private async Task OnPlayerMove(IContext context, ExtPlayerMoveRequest extPlayerMoveRequest)
        {
            this.LogDebug("Player attempting to move to position: {0},{1}", extPlayerMoveRequest.NewPosition.X, extPlayerMoveRequest.NewPosition.Y);
            
            if (currentMap == null)
            {
                this.LogWarning("Move failed: Player not in any map");
                await _sendToClient(new ExtMoveFailed(extPlayerMoveRequest.NewPosition, "Not in a map"));
                return;
            }

            pendingMove = extPlayerMoveRequest.NewPosition;
            context.Send(currentMap, new TryMove(context.Self, extPlayerMoveRequest.NewPosition, context.Self));
            await _sendToClient(new ExtMoveInitiated(extPlayerMoveRequest.NewPosition));
        }
    }
}
