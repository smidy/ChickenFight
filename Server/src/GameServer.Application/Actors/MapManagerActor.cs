﻿using GameServer.Application.Messages.Internal;
using GameServer.Application.Models;
using Proto;

namespace GameServer.Application.Actors
{
    public class MapManagerActor : IActor
    {
        private readonly Dictionary<string, (Map Map, PID Actor)> maps;

        public MapManagerActor()
        {
            maps = new Dictionary<string, (Map Map, PID Actor)>();
        }

        public Task ReceiveAsync(IContext context)
        {
            return context.Message switch
            {
                Started => OnStarted(context),
                RequestMapList msg => OnGetMapList(context, msg),
                _ => Task.CompletedTask
            };
        }

        private Task OnStarted(IContext context)
        {
            // Create initial maps
            CreateMap(context, "map1", "Small Arena", 32, 32);
            //CreateMap(context, "map2", "Large Arena", 64, 64);
            this.LogInformation("Created {0} initial maps", maps.Count);
            
            return Task.CompletedTask;
        }

        private Task OnGetMapList(IContext context, RequestMapList requestMapList)
        {
            context.Send(requestMapList.Requester, new MapListResponse(maps.Values.Select(m => m.Map).ToList()));
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
                    if (y == height / 2 || x == width / 2)
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
    }
}
