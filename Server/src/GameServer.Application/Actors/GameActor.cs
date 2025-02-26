using Proto;
using GameServer.Application.Models;
using GameServer.Application.Messages.Internal;

namespace GameServer.Application.Actors
{
    public class GameActor : IActor
    {
        private readonly Dictionary<string, (Map Map, PID Actor)> maps;
        private readonly Dictionary<PID, string> players;
        
        public GameActor()
        {
            maps = new Dictionary<string, (Map Map, PID Actor)>();
            players = new Dictionary<PID, string>();
        }

        public Task ReceiveAsync(IContext context)
        {
            return context.Message switch
            {
                Started => OnStarted(context),
                CreatePlayer msg => OnCreatePlayer(context, msg),
                GetMapList => OnGetMapList(context),
                _ => Task.CompletedTask
            };
        }

        private Task OnStarted(IContext context)
        {
            // Create initial maps
            CreateMap(context, "map1", "Small Arena", 32, 32);
            CreateMap(context, "map2", "Large Arena", 64, 64);
            return Task.CompletedTask;
        }

        private void CreateMap(IContext context, string id, string name, int width, int height)
        {
            // Create a sample tilemap - for now just alternating grass (0) and stone path (1)
            var tileData = new int[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // Create a simple pattern - a stone path through the middle
                    if (y == height/2 || x == width/2)
                    {
                        tileData[y * width + x] = 1; // stone path
                    }
                    else
                    {
                        tileData[y * width + x] = 0; // grass
                    }
                }
            }

            var map = new Map(id, name, width, height, tileData);
            var props = Props.FromProducer(() => new MapActor(id, name, width, height, tileData));
            var pid = context.SpawnNamed(props, id);
            maps[id] = (map, pid);
        }

        private Task OnCreatePlayer(IContext context, CreatePlayer msg)
        {
            // Create player actor with connectionId as its PlayerId/actor name
            var props = Props.FromProducer(() => new PlayerActor(msg.PlayerName, msg.SendToClient));
            var pid = context.SpawnNamed(props, msg.ConnectionId);
            var player = new Player(msg.PlayerName);
            
            players[pid] = msg.ConnectionId;
            
            context.Respond(new PlayerCreated(pid, player));
            return Task.CompletedTask;
        }

        private Task OnGetMapList(IContext context)
        {
            context.Respond(new MapList(maps.Values.Select(m => m.Map).ToList()));
            return Task.CompletedTask;
        }
    }
}
