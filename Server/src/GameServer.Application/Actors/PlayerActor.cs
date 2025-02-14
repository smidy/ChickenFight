using Proto;
using GameServer.Application.Models;
using GameServer.Application.Messages.Internal;
using GameServer.Shared.Messages;
using GameServer.Shared.Models;

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

            context.Send(foundMap, new AddPlayer(context.Self, player, context.Self));
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

            context.Send(currentMap, new RemovePlayer(player.Id, context.Self));
            await _sendToClient(new ExtLeaveMapInitiated(msg.MapId));
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

        private Task OnMapStateUpdate(IContext context, MapStateUpdate msg)
        {
            // Handle map state updates, potentially update local state or forward to client
            return Task.CompletedTask;
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
