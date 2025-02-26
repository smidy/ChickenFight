using System.Text;
using WsServer = NetCoreServer.WsServer;
using WsSession = NetCoreServer.WsSession;
using TcpSession = NetCoreServer.TcpSession;
using HttpRequest = NetCoreServer.HttpRequest;
using System.Net;
using Proto;
using GameServer.Shared;
using GameServer.Application.Messages.Internal;
using GameServer.Shared.ExternalMessages;

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

    public class GameSession : WsSession
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
                new CreatePlayer(sessionId, $"Player_{sessionId}", (ToClientMessage msg) => SendResponse((dynamic)msg))
            );
            playerActor = createResponse.PlayerActor;
            
            // Send connection confirmation to client
            await SendResponse(new OutConnectionConfirmed(createResponse.Player.Id));
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
                var baseMessage = JsonConfig.Deserialize<FromClientMessage>(message);

                switch (baseMessage)
                {
                    case InRequestMapList:
                        HandleRequestMapList();
                        break;
                    case InJoinMap joinMap:
                        HandleJoinMap(joinMap);
                        break;
                    case InLeaveMap leaveMap:
                        HandleLeaveMap(leaveMap);
                        break;
                    case InPlayerMove move:
                        HandleMove(move);
                        break;
                    case InFightChallengeSend challenge:
                        HandleFightChallenge(challenge);
                        break;
                    case InFightChallengeAccepted accept:
                        HandleFightChallengeAccepted(accept);
                        break;
                    case InPlayCard playCard:
                        HandlePlayCard(playCard);
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
                m.PlayerPositions.Count
            ));
            await SendResponse(new RequestMapListResponse(mapInfos.ToList()));
        }

        private async void HandleJoinMap(InJoinMap joinMap)
        {
            if (playerActor == null)
            {
                await SendResponse(new GameServer.Shared.ExternalMessages.OutJoinMapFailed(joinMap.MapId, "Player not connected"));
                return;
            }

            actorSystem.Root.Send(playerActor, new JoinMapRequest(joinMap.MapId));
        }

        private async void HandleLeaveMap(InLeaveMap leaveMap)
        {
            if (playerActor == null)
            {
                await SendResponse(new GameServer.Shared.ExternalMessages.OutLeaveMapFailed(leaveMap.MapId, "Not connected to any map"));
                return;
            }

            actorSystem.Root.Send(playerActor, new LeaveMapRequest(leaveMap.MapId));
        }

        private async void HandleFightChallengeAccepted(InFightChallengeAccepted accept)
        {
            if (playerActor == null)
            {
                // Cannot accept if not connected
                return;
            }

            actorSystem.Root.Send(playerActor, accept);
        }

        private async void HandleFightChallenge(InFightChallengeSend challenge)
        {
            if (playerActor == null)
            {
                // Cannot challenge if not connected
                return;
            }

            actorSystem.Root.Send(playerActor, challenge);
        }

        private async void HandleMove(InPlayerMove move)
        {
            if (playerActor == null)
            {
                await SendResponse(new OutPlayerInfo(null));
                return;
            }

            actorSystem.Root.Send(playerActor, new MoveRequest(move.NewPosition));
        }

        private async void HandlePlayCard(InPlayCard extPlayCard)
        {

            actorSystem.Root.Send(playerActor, extPlayCard);
        }
    }
}
