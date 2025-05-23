using System.Text;
using WsServer = NetCoreServer.WsServer;
using WsSession = NetCoreServer.WsSession;
using TcpSession = NetCoreServer.TcpSession;
using HttpRequest = NetCoreServer.HttpRequest;
using System.Net;
using Proto;
using GameServer.Shared;
using GameServer.Application.Messages.Internal;
using GameServer.Shared.Messages.Base;
using GameServer.Shared.Messages.Map;
using GameServer.Infrastructure;

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
        const int CreatePlayerTimeout = 5000;

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

        public override void OnWsConnected(HttpRequest request)
        {
            LoggingService.Logger.Information($"WebSocket session connected: {sessionId}");

            //TODO investigate other librarys to handle this async, for now wrap in Task.Run and wait with Result
            // Create player actor immediately on connection
            var createResponse = Task.Run(() => actorSystem.Root.RequestAsync<CreatePlayerResponse>(
                server.gameActor,
                new CreatePlayer(sessionId, $"Player_{sessionId}", (ExtServerMessage msg) => SendResponse((dynamic)msg)),
                TimeSpan.FromMilliseconds(CreatePlayerTimeout)
            )).Result;

            playerActor = createResponse.PlayerActor;           
        }

        public override void OnWsDisconnected()
        {
            LoggingService.Logger.Information($"WebSocket session disconnected: {sessionId}");

            if (playerActor != null)
            {
                // If player is in a map, ensure they leave properly
                actorSystem.Root.Send(playerActor, new ExtLeaveMapRequest(null)); // null mapId will force leave from any map

                // Stop the player actor
                actorSystem.Root.Poison(playerActor);
                playerActor = null;
            }
        }

        /// <summary>
        /// Send all incomming messages to the player actor which was created OnWsConnected 
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="size"></param>
        public override void OnWsReceived(byte[] buffer, long offset, long size)
        {
            try
            {
                //todo validate message security
                var message = Encoding.UTF8.GetString(buffer, (int)offset, (int)size);
                // Log incoming JSON message at debug level
                LoggingService.Logger.Debug($"Received message: {message}");
                var baseMessage = JsonConfig.Deserialize<ExtClientMessage>(message);
                actorSystem.Root.Send(playerActor, baseMessage);
            }
            catch (Exception ex)
            {
                LoggingService.Logger.Error($"Error processing message: {ex.Message}", ex);
            }
        }

        private Task SendResponse<T>(T response)
        {
            var json = JsonConfig.Serialize(response);
            // Log outgoing JSON message at debug level
            LoggingService.Logger.Debug($"Sending message: {json}");
            SendTextAsync(json);
            return Task.CompletedTask;
        }
    }
}
