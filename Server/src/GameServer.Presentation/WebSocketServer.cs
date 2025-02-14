using System;
using System.Text;
using System.Linq;
using NetCoreServer;
using TcpServer = NetCoreServer.TcpServer;
using WsServer = NetCoreServer.WsServer;
using WsSession = NetCoreServer.WsSession;
using TcpSession = NetCoreServer.TcpSession;
using HttpRequest = NetCoreServer.HttpRequest;
using System.Net;
using Proto;
using GameServer.Shared;
using GameServer.Application.Messages.Internal;
using GameServer.Shared.Messages;

namespace GameServer.Presentation
{
    public class GameWebSocketServer : WsServer
    {
        public readonly PID gameActor;
        public readonly ActorSystem actorSystem;

        public GameWebSocketServer(ActorSystem actorSystem, PID gameActor, string address, int port) 
            : base(IPAddress.Parse(address), port)
        {
            this.actorSystem = actorSystem;
            this.gameActor = gameActor;
        }

        protected override TcpSession CreateSession() => new GameSession(this);
    }

    public class GameSession : WsSession, IActor
    {
        private readonly GameWebSocketServer server;
        private readonly ActorSystem actorSystem;
        private PID? playerActor;
        private readonly string sessionId;
        private PID? self;

        public GameSession(WsServer server) : base(server)
        {
            this.server = (GameWebSocketServer)server;
            this.actorSystem = this.server.actorSystem;
            this.sessionId = Guid.NewGuid().ToString();
        }

        public override async void OnWsConnected(HttpRequest request)
        {
            Console.WriteLine($"WebSocket session connected: {sessionId}");
            
            // Create player actor immediately on connection
            var createResponse = await actorSystem.Root.RequestAsync<PlayerCreated>(
                server.gameActor,
                new CreatePlayer(sessionId, $"Player_{sessionId}", (BaseExternalMessage msg) => SendResponse((dynamic)msg))
            );
            playerActor = createResponse.PlayerActor;
            
            // Send connection confirmation to client
            await SendResponse(new ExtConnectionConfirmed(sessionId));
        }

        public override async void OnWsDisconnected()
        {
            Console.WriteLine($"WebSocket session disconnected: {sessionId}");

            if (playerActor != null)
            {
                // If player is in a map, ensure they leave properly
                actorSystem.Root.Send(playerActor, new LeaveMapRequest(null)); // null mapId will force leave from any map

                // Stop the player actor
                actorSystem.Root.Stop(playerActor);
                playerActor = null;
            }
        }

        public override void OnWsReceived(byte[] buffer, long offset, long size)
        {
            try
            {
                var message = Encoding.UTF8.GetString(buffer, (int)offset, (int)size);
                var baseMessage = JsonConfig.Deserialize<BaseExternalMessage>(message);

                switch (baseMessage?.Type)
                {
                    case nameof(ExtRequestMapList):
                        HandleRequestMapList();
                        break;
                    case nameof(ExtJoinMap):
                        var joinMap = JsonConfig.Deserialize<ExtJoinMap>(message);
                        HandleJoinMap(joinMap);
                        break;
                    case nameof(ExtLeaveMap):
                        var leaveMap = JsonConfig.Deserialize<ExtLeaveMap>(message);
                        HandleLeaveMap(leaveMap);
                        break;
                    case nameof(ExtMove):
                        var move = JsonConfig.Deserialize<ExtMove>(message);
                        HandleMove(move);
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing message: {ex.Message}");
            }
        }

        private Task SendResponse<T>(T response)
        {
            var json = JsonConfig.Serialize(response);
            SendTextAsync(json);
            return Task.CompletedTask;
        }

        private async void HandleRequestMapList()
        {
            var response = await actorSystem.Root.RequestAsync<MapList>(server.gameActor, new GetMapList());
            var mapInfos = response.Maps.Select(m => new MapInfo(
                m.Id, 
                m.Name, 
                m.Width, 
                m.Height, 
                m.Players.Count
            ));
            await SendResponse(new RequestMapListResponse(mapInfos.ToList()));
        }

        private async void HandleJoinMap(ExtJoinMap joinMap)
        {
            if (playerActor == null)
            {
                await SendResponse(new GameServer.Shared.Messages.ExtJoinMapFailed(joinMap.MapId, "Player not connected"));
                return;
            }

            actorSystem.Root.Send(playerActor, new JoinMapRequest(joinMap.MapId));
        }

        private async void HandleLeaveMap(ExtLeaveMap leaveMap)
        {
            if (playerActor == null)
            {
                await SendResponse(new GameServer.Shared.Messages.ExtLeaveMapFailed(leaveMap.MapId, "Not connected to any map"));
                return;
            }

            actorSystem.Root.Send(playerActor, new LeaveMapRequest(leaveMap.MapId));
        }

        private async void HandleMove(ExtMove move)
        {
            if (playerActor == null)
            {
                await SendResponse(new ExtPlayerInfo(null));
                return;
            }

            actorSystem.Root.Send(playerActor, new MoveRequest(move.NewPosition));
        }

        public Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case Started _:
                    self = context.Self;
                    break;

                // Map join messages
                case Application.Messages.Internal.JoinMapInitiated msg:
                    SendResponse(new GameServer.Shared.Messages.ExtJoinMapInitiated(msg.MapId));
                    break;
                case Application.Messages.Internal.JoinMapCompleted msg:
                    SendResponse(new GameServer.Shared.Messages.ExtJoinMapCompleted(msg.MapId, msg.TilemapData));
                    break;
                case Application.Messages.Internal.JoinMapFailed msg:
                    SendResponse(new GameServer.Shared.Messages.ExtJoinMapFailed(msg.MapId, msg.Error));
                    break;

                // Map leave messages
                case Application.Messages.Internal.LeaveMapInitiated msg:
                    SendResponse(new GameServer.Shared.Messages.ExtLeaveMapInitiated(msg.MapId));
                    break;
                case Application.Messages.Internal.LeaveMapCompleted msg:
                    SendResponse(new GameServer.Shared.Messages.ExtLeaveMapCompleted(msg.MapId));
                    break;
                case Application.Messages.Internal.LeaveMapFailed msg:
                    SendResponse(new GameServer.Shared.Messages.ExtLeaveMapFailed(msg.MapId, msg.Error));
                    break;

                // Movement messages
                case Application.Messages.Internal.MoveInitiated msg:
                    SendResponse(new GameServer.Shared.Messages.ExtMoveInitiated(msg.NewPosition));
                    break;
                case Application.Messages.Internal.MoveCompleted msg:
                    SendResponse(new GameServer.Shared.Messages.ExtMoveCompleted(msg.NewPosition));
                    break;
                case Application.Messages.Internal.MoveFailed msg:
                    SendResponse(new GameServer.Shared.Messages.ExtMoveFailed(msg.AttemptedPosition, msg.Error));
                    break;

                // Player state messages
                case Application.Messages.Internal.PlayerStateUpdate msg:
                    SendResponse(new ExtPlayerInfo(new PlayerState(msg.Player.Id, msg.Player.Name, msg.Player.Position)));
                    break;
            }

            return Task.CompletedTask;
        }
    }
}
