using Proto;
using GameServer.Application.Models;
using GameServer.Application.Messages.Internal;

namespace GameServer.Application.Actors
{
    public class MapActor : IActor
    {
        private readonly Map map;
        private readonly HashSet<PID> subscribers;

        public MapActor(string id, string name, int width, int height, int[] tileData)
        {
            map = new Map(id, name, width, height, tileData);
            subscribers = new HashSet<PID>();
        }

        public Task ReceiveAsync(IContext context)
        {
            return context.Message switch
            {
                Started => OnStarted(context),
                AddPlayer msg => OnAddPlayer(context, msg),
                RemovePlayer msg => OnRemovePlayer(context, msg),
                ValidateMove msg => OnValidateMove(context, msg),
                SubscribeToMapUpdates msg => OnSubscribeToMapUpdates(context, msg),
                UnsubscribeFromMapUpdates msg => OnUnsubscribeFromMapUpdates(context, msg),
                _ => Task.CompletedTask
            };
        }

        private Task OnStarted(IContext context) => Task.CompletedTask;

        private Task OnAddPlayer(IContext context, AddPlayer msg)
        {
            if (map.TryAddPlayer(msg.Player, out var startPosition))
            {
                subscribers.Add(msg.PlayerActor);
                BroadcastMapUpdate(context);
                var tilemapData = new GameServer.Shared.Messages.TilemapData(map.Width, map.Height, map.TileData);
                context.Send(msg.Requester, new PlayerAddedToMap(msg.PlayerActor, startPosition, tilemapData));
            }
            else
            {
                context.Send(msg.Requester, new PlayerAddFailure(msg.PlayerActor, "Map is full"));
            }
            return Task.CompletedTask;
        }

        private Task OnRemovePlayer(IContext context, RemovePlayer msg)
        {
            if (map.RemovePlayer(msg.PlayerId))
            {
                subscribers.Remove(context.Sender);
                BroadcastMapUpdate(context);
                context.Send(msg.Requester, new PlayerRemovedFromMap(msg.PlayerId));
            }
            else
            {
                context.Send(msg.Requester, new PlayerRemoveFailure(msg.PlayerId, "Player not found in map"));
            }
            return Task.CompletedTask;
        }

        private Task OnValidateMove(IContext context, ValidateMove msg)
        {
            if (map.TryMovePlayer(msg.PlayerId, msg.NewPosition))
            {
                BroadcastMapUpdate(context);
                context.Send(msg.Requester, new MoveValidated(msg.PlayerId, msg.NewPosition));
            }
            else
            {
                context.Send(msg.Requester, new MoveRejected(msg.PlayerId, msg.NewPosition, "Invalid move"));
            }
            return Task.CompletedTask;
        }

        private Task OnSubscribeToMapUpdates(IContext context, SubscribeToMapUpdates msg)
        {
            subscribers.Add(msg.Subscriber);
            context.Send(msg.Subscriber, new MapStateUpdate(map));
            context.Send(msg.Subscriber, new MapUpdateSubscribed(msg.Subscriber));
            return Task.CompletedTask;
        }

        private Task OnUnsubscribeFromMapUpdates(IContext context, UnsubscribeFromMapUpdates msg)
        {
            subscribers.Remove(msg.Subscriber);
            context.Send(msg.Subscriber, new MapUpdateUnsubscribed(msg.Subscriber));
            return Task.CompletedTask;
        }

        private void BroadcastMapUpdate(IContext context)
        {
            var update = new MapStateUpdate(map);
            foreach (var subscriber in subscribers)
            {
                context.Send(subscriber, update);
            }
        }
    }
}
