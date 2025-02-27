using Proto;
using GameServer.Application.Messages.Internal;

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
            var props = Props.FromProducer(() => new MapManagerActor());
            mapManager = context.SpawnNamed(props, "MapManager");
            return Task.CompletedTask;
        }

        private Task OnCreatePlayer(IContext context, CreatePlayer msg)
        {
            var props = Props.FromProducer(() => new PlayerActor(mapManager, msg.PlayerName, msg.SendToClient));
            var pid = context.SpawnNamed(props, msg.PlayerId);
            
            context.Respond(new CreatePlayerResponse(pid));
            return Task.CompletedTask;
        }
    }
}
