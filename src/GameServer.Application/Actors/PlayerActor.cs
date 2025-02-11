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
        private readonly SendToClientDelegate<object> _sendToClient;
        private PID? currentMap;
        private string? pendingMapJoin;
        private Position? pendingMove;

        public PlayerActor(string connectionId, string playerName, SendToClientDelegate<object> sendToClient)
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
                
                // Map PID resolution messages
                MapPidResolved msg => OnMapPidResolved(context, msg),
                MapPidNotFound msg => OnMapPidNotFound(context, msg),
                
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
                
                _ => Task.CompletedTask
            };
        }

        private Task OnStarted(IContext context) => Task.CompletedTask;

        private Task OnJoinMapRequest(IContext context, JoinMapRequest msg)
        {
            if (currentMap != null)
            {
                context.Send(context.Sender, new JoinMapResponse(false, null, "Already in a map"));
                return Task.CompletedTask;
            }

            pendingMapJoin = msg.MapId;
            context.Send(context.Parent, new GetMapPid(msg.MapId, context.Self));
            context.Send(context.Sender, new JoinMapInitiated(msg.MapId));
            return Task.CompletedTask;
        }

        private Task OnMapPidResolved(IContext context, MapPidResolved msg)
        {
            if (msg.MapId != pendingMapJoin || msg.MapPid == null)
            {
                context.Send(context.Sender, new JoinMapFailed(msg.MapId, "Map not found"));
                pendingMapJoin = null;
                return Task.CompletedTask;
            }

            context.Send(msg.MapPid, new AddPlayer(context.Self, player, context.Self));
            return Task.CompletedTask;
        }

        private Task OnMapPidNotFound(IContext context, MapPidNotFound msg)
        {
            if (msg.MapId == pendingMapJoin)
            {
                context.Send(context.Sender, new JoinMapFailed(msg.MapId, "Map not found"));
                pendingMapJoin = null;
            }
            return Task.CompletedTask;
        }

        private Task OnPlayerAddedToMap(IContext context, PlayerAddedToMap msg)
        {
            if (pendingMapJoin != null)
            {
                currentMap = context.Sender;
                player.JoinMap(pendingMapJoin, msg.StartPosition);
                context.Send(context.Parent, new JoinMapCompleted(pendingMapJoin, msg.TilemapData));
                pendingMapJoin = null;
            }
            return Task.CompletedTask;
        }

        private Task OnPlayerAddFailure(IContext context, PlayerAddFailure msg)
        {
            if (pendingMapJoin != null)
            {
                context.Send(context.Parent, new JoinMapFailed(pendingMapJoin, msg.Error));
                pendingMapJoin = null;
            }
            return Task.CompletedTask;
        }

        private Task OnLeaveMapRequest(IContext context, LeaveMapRequest msg)
        {
            // If mapId is null, force leave from current map (used for disconnection)
            if (currentMap == null || (msg.MapId != null && msg.MapId != player.CurrentMapId))
            {
                context.Send(context.Sender, new LeaveMapResponse(true));
                return Task.CompletedTask;
            }

            context.Send(currentMap, new RemovePlayer(player.Id, context.Self));
            context.Send(context.Sender, new LeaveMapInitiated(player.CurrentMapId));
            return Task.CompletedTask;
        }

        private Task OnPlayerRemovedFromMap(IContext context, PlayerRemovedFromMap msg)
        {
            if (msg.PlayerId == player.Id)
            {
                player.LeaveMap();
                currentMap = null;
                context.Send(context.Parent, new LeaveMapCompleted(msg.PlayerId));
            }
            return Task.CompletedTask;
        }

        private Task OnPlayerRemoveFailure(IContext context, PlayerRemoveFailure msg)
        {
            if (msg.PlayerId == player.Id)
            {
                context.Send(context.Parent, new LeaveMapFailed(msg.PlayerId, msg.Error));
            }
            return Task.CompletedTask;
        }

        private Task OnMoveRequest(IContext context, MoveRequest msg)
        {
            if (currentMap == null)
            {
                context.Send(context.Sender, new MoveResponse(false, "Not in a map"));
                return Task.CompletedTask;
            }

            pendingMove = msg.NewPosition;
            context.Send(currentMap, new ValidateMove(player.Id, msg.NewPosition, context.Self));
            context.Send(context.Sender, new MoveInitiated(msg.NewPosition));
            return Task.CompletedTask;
        }

        private async Task OnMoveValidated(IContext context, MoveValidated msg)
        {
            if (msg.PlayerId == player.Id && 
                pendingMove?.X == msg.NewPosition.X && 
                pendingMove?.Y == msg.NewPosition.Y)
            {
                player.UpdatePosition(msg.NewPosition);
                await _sendToClient(new PlayerInfo(new PlayerState(player.Id, player.Name, player.Position)));
                context.Send(context.Parent, new MoveCompleted(msg.NewPosition));
                pendingMove = null;
            }
        }

        private Task OnMoveRejected(IContext context, MoveRejected msg)
        {
            if (msg.PlayerId == player.Id && 
                pendingMove?.X == msg.AttemptedPosition.X && 
                pendingMove?.Y == msg.AttemptedPosition.Y)
            {
                context.Send(context.Parent, new MoveFailed(msg.AttemptedPosition, msg.Error));
                pendingMove = null;
            }
            return Task.CompletedTask;
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
