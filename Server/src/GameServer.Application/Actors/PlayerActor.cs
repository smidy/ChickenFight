using Proto;
using GameServer.Application.Models;
using GameServer.Application.Messages.Internal;
using GameServer.Shared.Models;
using GameServer.Shared.ExternalMessages;

namespace GameServer.Application.Actors
{
    public delegate Task SendToClientDelegate<in T>(T message) where T : ToClientMessage;

    /// <summary>
    /// This actor represents a player in the game. 
    /// It recieves messages from the client and other Actors in the Actor system.
    /// It sends 'ToClientMessage' messages to the players client using the delegate method _sendToClient
    /// </summary>
    public class PlayerActor : IActor
    {
        private readonly Player player;
        private readonly SendToClientDelegate<ToClientMessage> _sendToClient;
        private PID? currentMap;
        private string? pendingMapJoin;
        private ExPosition? pendingMove;

        public PlayerActor(string playerName, SendToClientDelegate<ToClientMessage> sendToClient)
        {
            player = new Player(playerName);
            _sendToClient = sendToClient;
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

                // Fight messages
                InFightChallengeSend msg => OnFightChallengeSend(context, msg),
                FightChallengeRequest msg => OnFightChallengeRequestReceived(context, msg),
                OutFightStarted msg => OnFightStarted(context, msg),
                OutFightEnded msg => OnFightEnded(context, msg),

                //Send external messages to client
                ToClientMessage msg => _sendToClient(msg),

                _ => Task.CompletedTask
            };
        }

        private async Task OnStarted(IContext context) {
            //Set the Player.Id as the Actor name/Id
            this.player.SetId(context.Self.Id);
        }

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
                return;
            }

            context.Send(foundMap, new AddPlayer(context.Self, player.Name, context.Self));
            await _sendToClient(new OutJoinMapInitiated(msg.MapId));
        }

        private PID? GetMapPid(IContext context, string actorName)
        {
            return context.System.ProcessRegistry.Find(x => x == $"$1/{actorName}").FirstOrDefault();
        }

        private async Task OnPlayerAddedToMap(IContext context, PlayerAddedToMap msg)
        {
            if (pendingMapJoin != null && msg.PlayerActor.Equals(context.Self))
            {
                currentMap = msg.MapPID;
                player.JoinMap(pendingMapJoin, msg.StartPosition);
                var exPlayerPositions = msg.PlayerPositions.ToDictionary(y => y.Key.Id, y => new ExPosition(y.Value.X, y.Value.Y));
                await _sendToClient(new OutJoinMapCompleted(msg.MapId, player.Id, msg.StartPosition, msg.TilemapData, exPlayerPositions));
                pendingMapJoin = null;
            }
        }

        private async Task OnPlayerAddFailure(IContext context, PlayerAddFailure msg)
        {
            if (pendingMapJoin != null && msg.PlayerActor.Equals(context.Self))
            {
                await _sendToClient(new OutJoinMapFailed(msg.MapId, msg.Error));
                pendingMapJoin = null;
            }
        }

        private async Task OnLeaveMapRequest(IContext context, LeaveMapRequest msg)
        {
            // If mapId is null, force leave from current map (used for disconnection)
            if (currentMap == null)
            {
                await _sendToClient(new OutLeaveMapFailed(msg.MapId, "Player has not joined a map"));
                return;
            }
            if (msg.MapId != null && msg.MapId != player.CurrentMapId)
            {
                await _sendToClient(new OutLeaveMapFailed(msg.MapId, "Invalid map id specified"));
                return;
            }
            if (player.IsInFight && msg.MapId != null) // Allow force disconnect even in fight
            {
                await _sendToClient(new OutLeaveMapFailed(msg.MapId, "Cannot leave map while in a fight"));
                return;
            }

            context.Send(currentMap, new RemovePlayer(context.Self, context.Self));
            await _sendToClient(new OutLeaveMapInitiated(msg.MapId));
        }

        private async Task OnFightChallengeSend(IContext context, InFightChallengeSend msg)
        {
            if (currentMap == null)
                return;

            // Find target PID using the actor system
            var targetPid = context.System.ProcessRegistry.Find(x => x == msg.TargetId).FirstOrDefault();
            if (targetPid != null)
            {
                context.Send(currentMap, new FightChallengeRequest(context.Self, player, targetPid));
            }
        }

        private async Task OnFightChallengeRequestReceived(IContext context, FightChallengeRequest msg)
        {
            if (currentMap == null)
                return;

            // Auto-accept for now - in a real implementation, you'd wait for player input
            context.Send(currentMap, new FightChallengeResponse(msg.ChallengerActor, msg.Challenger, context.Self, player, true));
            await _sendToClient(new OutFightChallengeAccepted(msg.ChallengerActor.Id));
        }

        private async Task OnFightStarted(IContext context, OutFightStarted msg)
        {
            await _sendToClient(msg);
        }

        private async Task OnFightEnded(IContext context, OutFightEnded msg)
        {
            await _sendToClient(msg);
        }

        private async Task OnPlayerRemovedFromMap(IContext context, PlayerRemovedFromMap msg)
        {
            if (msg.PlayerActor.Equals(context.Self))
            {
                player.LeaveMap();
                currentMap = null;
                await _sendToClient(new OutLeaveMapCompleted(msg.MapId));
            }
        }

        private async Task OnPlayerRemoveFailure(IContext context, PlayerRemoveFailure msg)
        {
            if (msg.PlayerActor.Equals(context.Self))
            {
                await _sendToClient(new OutLeaveMapFailed(msg.MapId, msg.Error));
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
            context.Send(currentMap, new ValidateMove(context.Self, msg.NewPosition, context.Self));
            await _sendToClient(new OutMoveInitiated(msg.NewPosition));
        }

        private async Task OnMoveValidated(IContext context, MoveValidated msg)
        {
            if (msg.PlayerActor.Equals(context.Self) && 
                pendingMove?.X == msg.NewPosition.X && 
                pendingMove?.Y == msg.NewPosition.Y)
            {
                player.UpdatePosition(msg.NewPosition);
                await _sendToClient(new OutPlayerInfo(new PlayerState(player.Id, player.Name, player.Position)));
                await _sendToClient(new OutMoveCompleted(msg.NewPosition));
                pendingMove = null;
            }
        }

        private async Task OnMoveRejected(IContext context, MoveRejected msg)
        {
            if (msg.PlayerActor.Equals(context.Self) && 
                pendingMove?.X == msg.AttemptedPosition.X && 
                pendingMove?.Y == msg.AttemptedPosition.Y)
            {
                await _sendToClient(new OutMoveFailed(msg.AttemptedPosition, msg.Error));
                pendingMove = null;
            }
        }

        private Task OnMapStateUpdate(IContext context, MapStateUpdate msg)
        {
            return Task.CompletedTask;
        }
    }
}
