using Proto;
using GameServer.Application.Models;
using GameServer.Application.Messages.Internal;
using GameServer.Shared.ExternalMessages;

namespace GameServer.Application.Actors
{
    /// <summary>
    /// This actor will represent a fight between two players. 
    /// A fight begins when one player challenges another. 
    /// For a player to challenge or be challenged they must not be currently in a fight. 
    /// While players are fighting they cannot move or leave a map unless they disconnect from the game at which point they lose the fight.
    /// </summary>
    public class FightActor : IActor
    {
        private readonly string fightId;
        private readonly string player1Id;
        private readonly string player2Id;
        private readonly PID mapActor;
        private bool isActive;

        public FightActor(string fightId, string player1Id, string player2Id, PID mapActor)
        {
            this.fightId = fightId;
            this.player1Id = player1Id;
            this.player2Id = player2Id;
            this.mapActor = mapActor;
            this.isActive = true;
        }

        public Task ReceiveAsync(IContext context)
        {
            return context.Message switch
            {
                Started => OnStarted(context),
                PlayerDisconnected msg => OnPlayerDisconnected(context, msg),
                EndFight msg => OnEndFight(context, msg),
                _ => Task.CompletedTask
            };
        }

        private Task OnStarted(IContext context)
        {
            context.Send(mapActor, new FightStarted(fightId, player1Id, player2Id));
            return Task.CompletedTask;
        }

        private Task OnPlayerDisconnected(IContext context, PlayerDisconnected msg)
        {
            if (!isActive) return Task.CompletedTask;

            string winnerId = msg.PlayerId == player1Id ? player2Id : player1Id;
            string loserId = msg.PlayerId;
            
            isActive = false;
            context.Send(mapActor, new FightCompleted(fightId, winnerId, loserId, "Player disconnected"));
            return Task.CompletedTask;
        }

        private Task OnEndFight(IContext context, EndFight msg)
        {
            if (!isActive) return Task.CompletedTask;

            isActive = false;
            context.Send(mapActor, new FightCompleted(fightId, msg.WinnerId, msg.LoserId, msg.Reason));
            return Task.CompletedTask;
        }
    }
}
