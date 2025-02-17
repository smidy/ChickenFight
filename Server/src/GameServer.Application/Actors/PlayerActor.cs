using Proto;
using GameServer.Application.Models;
using GameServer.Application.Messages.Internal;
using GameServer.Shared.Models;
using GameServer.Shared.ExternalMessages;

namespace GameServer.Application.Actors
{
    public class PlayerActor : IActor
    {
        private readonly string connectionId;
        private readonly Player player;
        private readonly SendToClientDelegate<BaseExternalMessage> _sendToClient;
        private PID? currentMap;
        private string? pendingMapJoin;
        private Position? pendingMove;

        public PlayerActor(string connectionId, string playerName, SendToClientDelegate<BaseExternalMessage> sendToClient)
        {
            this.connectionId = connectionId;
            this.player = new Player(connectionId, playerName);
            this._sendToClient = sendToClient;
        }

        public Task ReceiveAsync(IContext context)
        {
            return context.Message switch
            {
                Started => OnStarted(context),
                JoinMapRequest msg => OnJoinMapRequest(context, msg),
                LeaveMapRequest msg => OnLeaveMapRequest(context, msg),
                MoveRequest msg => OnMoveRequest(context, msg),
                               
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
                MapUpdateSubscribed msg => OnMapUpdateSubscribed(context, msg),
                MapUpdateUnsubscribed msg => OnMapUpdateUnsubscribed(context, msg),

                // Fight messages
                ExtFightChallengeReceived msg => OnFightChallengeReceived(context, msg),
                ExtFightStarted msg => OnFightStarted(context, msg),
                ExtFightEnded msg => OnFightEnded(context, msg),

                //Send external messages to client
                BaseExternalMessage msg => _sendToClient(msg),

                _ => Task.CompletedTask
            };
        }

        private Task OnStarted(IContext context) => Task.CompletedTask;

        private async Task OnJoinMapRequest(IContext context, JoinMapRequest msg)
        {
            if (currentMap != null)
            {
                context.Send(context.Sender, new GameServer.Application.Messages.Internal.JoinMapFailed(msg.MapId, "Already in a map"));
            }

            pendingMapJoin = msg.MapId;

            var foundMap = GetMapPid(context, msg.MapId);

            if(foundMap == null)
            {
                context.Send(context.Sender, new GameServer.Application.Messages.Internal.JoinMapFailed(msg.MapId, "Invalid Map Id"));
            }

            context.Send(foundMap, new AddPlayer(context.Self, player.Id, player.Name, context.Self));
            await _sendToClient(new ExtJoinMapInitiated(msg.MapId));
        }

        private PID? GetMapPid(IContext context, string actorName)
        {
            return context.System.ProcessRegistry.Find(x => x == $"$1/{actorName}").FirstOrDefault();
        }

        private async Task OnPlayerAddedToMap(IContext context, PlayerAddedToMap msg)
        {
            if (pendingMapJoin != null)
            {
                currentMap = msg.MapPID;
                player.JoinMap(pendingMapJoin, msg.StartPosition);
                await _sendToClient(new ExtJoinMapCompleted(msg.MapId, msg.TilemapData));
                pendingMapJoin = null;
            }
        }

        private async Task OnPlayerAddFailure(IContext context, PlayerAddFailure msg)
        {
            if (pendingMapJoin != null)
            {
                await _sendToClient(new ExtJoinMapFailed(msg.MapId, msg.Error));
                pendingMapJoin = null;
            }
        }

        private async Task OnLeaveMapRequest(IContext context, LeaveMapRequest msg)
        {
            // If mapId is null, force leave from current map (used for disconnection)
            if (currentMap == null)
            {
                await _sendToClient(new ExtLeaveMapFailed(msg.MapId, "Player has not joined a map"));
                return;
            }
            if (msg.MapId != null && msg.MapId != player.CurrentMapId)
            {
                await _sendToClient(new ExtLeaveMapFailed(msg.MapId, "Invalid map id specified"));
                return;
            }
            if (player.IsInFight && msg.MapId != null) // Allow force disconnect even in fight
            {
                await _sendToClient(new ExtLeaveMapFailed(msg.MapId, "Cannot leave map while in a fight"));
                return;
            }

            context.Send(currentMap, new RemovePlayer(player.Id, context.Self));
            await _sendToClient(new ExtLeaveMapInitiated(msg.MapId));
        }

        private async Task OnFightChallengeReceived(IContext context, ExtFightChallengeReceived msg)
        {
            if (currentMap == null)
                return;

            // Auto-accept for now - in a real implementation, you'd wait for player input
            context.Send(currentMap, new FightChallengeResponse(msg.ChallengerId, player.Id, true));
            await _sendToClient(new ExtFightChallengeAccepted(msg.ChallengerId));
        }

        private async Task OnFightStarted(IContext context, ExtFightStarted msg)
        {
            await _sendToClient(msg);
        }

        private async Task OnFightEnded(IContext context, ExtFightEnded msg)
        {
            await _sendToClient(msg);
        }

        private async Task OnPlayerRemovedFromMap(IContext context, PlayerRemovedFromMap msg)
        {
            if (msg.PlayerId == player.Id)
            {
                player.LeaveMap();
                currentMap = null;
                await _sendToClient(new ExtLeaveMapCompleted(msg.MapId));
            }
        }

        private async Task OnPlayerRemoveFailure(IContext context, PlayerRemoveFailure msg)
        {
            if (msg.PlayerId == player.Id)
            {
                await _sendToClient(new ExtLeaveMapFailed(msg.MapId, msg.Error));
            }
        }

        private async Task OnMoveRequest(IContext context, MoveRequest msg)
        {
            if (currentMap == null)
            {
                context.Send(context.Sender, new Messages.Internal.MoveFailed(msg.NewPosition, "Not in a map"));
                return;
            }

            pendingMove = msg.NewPosition;
            context.Send(currentMap, new ValidateMove(player.Id, msg.NewPosition, context.Self));
            await _sendToClient(new ExtMoveInitiated(msg.NewPosition));
        }

        private async Task OnMoveValidated(IContext context, MoveValidated msg)
        {
            if (msg.PlayerId == player.Id && 
                pendingMove?.X == msg.NewPosition.X && 
                pendingMove?.Y == msg.NewPosition.Y)
            {
                player.UpdatePosition(msg.NewPosition);
                await _sendToClient(new ExtPlayerInfo(new PlayerState(player.Id, player.Name, player.Position)));
                await _sendToClient(new ExtMoveCompleted(msg.NewPosition));
                pendingMove = null;
            }
        }

        private async Task OnMoveRejected(IContext context, MoveRejected msg)
        {
            if (msg.PlayerId == player.Id && 
                pendingMove?.X == msg.AttemptedPosition.X && 
                pendingMove?.Y == msg.AttemptedPosition.Y)
            {
                await _sendToClient(new ExtMoveFailed(msg.AttemptedPosition, msg.Error));
                pendingMove = null;
            }
        }

        private async Task OnMapStateUpdate(IContext context, MapStateUpdate msg)
        {
            // Update player position if it exists in the map
            if (msg.PlayerPositions.TryGetValue(player.Id, out var position))
            {
                player.UpdatePosition(position);
                await _sendToClient(new ExtPlayerInfo(new PlayerState(player.Id, player.Name, player.Position)));
            }
        }

        private Task OnMapUpdateSubscribed(IContext context, MapUpdateSubscribed msg)
        {
            // Handle subscription confirmation
            return Task.CompletedTask;
        }

        private Task OnMapUpdateUnsubscribed(IContext context, MapUpdateUnsubscribed msg)
        {
            // Handle unsubscription confirmation
            return Task.CompletedTask;
        }
    }
}
