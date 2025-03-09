using Proto;
using GameServer.Application.Messages.Internal;
using Microsoft.Extensions.Logging;
using Proto.Logging;
using GameServer.Application.Extensions;
using GameServer.Infrastructure;

namespace GameServer.Application.Actors
{
    public class GameActor : IActor
    {
        private PID mapManager;

        public GameActor()
        {

        }

        public Task ReceiveAsync(IContext context)
        {
            return context.Message switch
            {
                Started => OnStarted(context),
                CreatePlayer msg => OnCreatePlayer(context, msg),
                _ => Task.CompletedTask
            };
        }

        private Task OnStarted(IContext context)
        {
            this.LogInformation("Game actor started");
            var props = Props.FromProducer(() => new MapManagerActor());
            mapManager = context.SpawnNamed(props, "MapManager");
            this.LogInformation("Map manager actor created");
            return Task.CompletedTask;
        }

        private Task OnCreatePlayer(IContext context, CreatePlayer msg)
        {
            this.LogInformation("Creating new player actor with ID: {0}, Name: {1}", msg.PlayerId, msg.PlayerName);
            var props = Props.FromProducer(() => new PlayerActor(mapManager, msg.PlayerName, msg.SendToClient));
            var pid = context.SpawnNamed(props, msg.PlayerId);
            
            this.LogInformation("Player actor created with PID: {0}", pid.Id);
            context.Respond(new CreatePlayerResponse(pid));
            return Task.CompletedTask;
        }
    }
}
