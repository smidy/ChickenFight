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
        private readonly PID mapManagerActor;
        private readonly SendToClientDelegate<ToClientMessage> _sendToClient;
        private PID? currentMap;
        private PID? currentFight;
        private string? pendingMapJoin;
        private MapPosition? pendingMove;

        public PlayerActor(PID mapManagerActor, string playerName, SendToClientDelegate<ToClientMessage> sendToClient)
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

                FromClientMessage msg => OnIncommingClientMessage(context, msg),

                //Send external messages to client
                ToClientMessage msg => OnOutgoingClientMessage(context, msg),

                _ => Task.CompletedTask
            };
        }

        private async Task OnStarted(IContext context) {
            //Set the Player.Id as the Actor name/Id
            this.player.SetId(context.Self.Id);
            await _sendToClient(new OutConnectionConfirmed(this.player.Id));
        }

        private PID? GetMapPid(IContext context, string actorName)
        {
            return context.System.ProcessRegistry.Find(x => x == $"$1/MapManager/{actorName}").FirstOrDefault();
        }

        private async Task OnPlayerAddedToMap(IContext context, PlayerAddedToMap msg)
        {
            if (pendingMapJoin != null && msg.PlayerActor.Equals(context.Self))
            {
                currentMap = msg.MapPID;
                player.JoinMap(pendingMapJoin, msg.StartPosition);
                var exPlayerPositions = msg.PlayerPositions.ToDictionary(y => y.Key.Id, y => new MapPosition(y.Value.X, y.Value.Y));
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
        }

        private async Task OnFightStarted(IContext context, FightStarted msg)
        {
            currentFight = msg.FightActor;
            var opponentId = msg.Player1 == this.player ? msg.Player1.Id : msg.Player2.Id;
            await _sendToClient(new OutFightStarted(opponentId));
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

        private async Task OnMapListResponse(IContext context, MapListResponse msg)
        {
            var mapInfos = msg.Maps.Select(m => new MapInfo(
                m.Id,
                m.Name,
                m.Width,
                m.Height,
                m.PlayerPositions.Count
            ));
            await _sendToClient(new OutRequestMapListResponse(mapInfos.ToList()));
        }

        private async Task OnMapStateUpdate(IContext context, MapStateUpdate msg)
        {

        }

        private async Task OnPlayCard(IContext context, InPlayCard msg)
        {
            if (currentFight != null)
            {
                context.Send(currentFight, new PlayCard(context.Self, msg.CardId));
            }
        }

        private async Task OnEndTurn(IContext context, InEndTurn msg)
        {
            if (currentFight != null)
            {
                context.Send(currentFight, new EndTurn(context.Self));
            }
        }

        private async Task OnIncommingClientMessage(IContext context, FromClientMessage fromClientMessage)
        {
            var handle = fromClientMessage switch
            {
                InRequestMapList msg => OnRequestMapList(context, msg),
                InJoinMap msg => OnJoinMap(context, msg),
                InLeaveMap msg => OnLeaveMap(context, msg),
                InPlayerMove msg => OnPlayerMove(context, msg),
                InFightChallengeSend msg => OnFightChallengeSend(context, msg),
                InPlayCard msg => OnPlayCard(context, msg),
                InEndTurn msg => OnEndTurn(context, msg),
                _ => Task.CompletedTask
            };
            await handle;
        }

        private async Task OnOutgoingClientMessage(IContext context, ToClientMessage toClientMessage)
        {
            await _sendToClient(toClientMessage);
        }

        private async Task OnRequestMapList(IContext context, InRequestMapList inRequestMapList)
        {
            context.Send(mapManagerActor, new RequestMapList(context.Self));
        }

        private async Task OnJoinMap(IContext context, InJoinMap inJoinMap)
        {
            if (currentMap != null)
            {
                await _sendToClient(new OutJoinMapFailed(inJoinMap.MapId, "Already in a map"));
            }

            pendingMapJoin = inJoinMap.MapId;

            var foundMap = GetMapPid(context, inJoinMap.MapId);

            if (foundMap == null)
            {
                await _sendToClient(new OutJoinMapFailed(inJoinMap.MapId, "Invalid Map Id"));
                return;
            }

            context.Send(foundMap, new JoinMap(context.Self, player.Name, context.Self));
            await _sendToClient(new OutJoinMapInitiated(inJoinMap.MapId));
        }

        private async Task OnLeaveMap(IContext context, InLeaveMap inLeaveMap)
        {
            if (currentMap == null)
            {
                await _sendToClient(new OutLeaveMapFailed(inLeaveMap.MapId, "Player has not joined a map"));
                return;
            }
            if (inLeaveMap.MapId != null && inLeaveMap.MapId != player.CurrentMapId)
            {
                await _sendToClient(new OutLeaveMapFailed(inLeaveMap.MapId, "Invalid map id specified"));
                return;
            }
            if (player.IsInFight && inLeaveMap.MapId != null)
            {
                await _sendToClient(new OutLeaveMapFailed(inLeaveMap.MapId, "Cannot leave map while in a fight"));
                return;
            }

            context.Send(currentMap, new LeaveMap(context.Self, context.Self));
            await _sendToClient(new OutLeaveMapInitiated(inLeaveMap.MapId));
        }

        private async Task OnPlayerMove(IContext context, InPlayerMove inPlayerMove)
        {
            if (currentMap == null)
            {
                await _sendToClient(new OutMoveFailed(inPlayerMove.NewPosition, "Not in a map"));
                return;
            }

            pendingMove = inPlayerMove.NewPosition;
            context.Send(currentMap, new TryMove(context.Self, inPlayerMove.NewPosition, context.Self));
            await _sendToClient(new OutMoveInitiated(inPlayerMove.NewPosition));
        }
    }
}
