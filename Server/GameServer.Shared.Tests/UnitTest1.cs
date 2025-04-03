using GameServer.Shared.Messages.CardBattle;
using GameServer.Shared.Messages.Connection;
using GameServer.Shared.Messages.Fight;
using GameServer.Shared.Messages.Map;

namespace GameServer.Shared.Tests
{
    public class UnitTest1
    {
        [Fact]
        public async Task Test1()
        {
            var gameContext = new GameStateContext("ws://127.0.0.1:8080");
            await gameContext.Connect();
            gameContext.Send(new ExtPlayerIdRequest());

            var gameContext2 = new GameStateContext("ws://127.0.0.1:8080");
            await gameContext2.Connect();
            gameContext2.Send(new ExtPlayerIdRequest());
            await Task.Delay(3000);

            gameContext.Send(new ExtJoinMapRequest("map1"));
            gameContext2.Send(new ExtJoinMapRequest("map1"));
            await Task.Delay(3000);

            for (int i = 0; i < 5; i++)
            {
                gameContext.Send(new ExtFightChallengeRequest(gameContext2.PlayerId));
                await Task.Delay(3000);

                while (gameContext.IsInFight)
                {
                    if (gameContext.IsPlayerTurn)
                    {
                        var possibleCard = gameContext.CardsInHand.FirstOrDefault(x => x.Cost <= gameContext.PlayerActionPoints);
                        if (possibleCard != null)
                        {
                            gameContext.Send(new ExtPlayCardRequest(possibleCard.Id));
                        }

                        gameContext.Send(new ExtEndTurnRequest());
                    }
                    else
                    {
                        var possibleCard = gameContext2.CardsInHand.FirstOrDefault(x => x.Cost <= gameContext2.PlayerActionPoints);
                        if (possibleCard != null)
                        {
                            gameContext2.Send(new ExtPlayCardRequest(possibleCard.Id));
                        }

                        gameContext2.Send(new ExtEndTurnRequest());
                    }
                    await Task.Delay(1000);
                }

            }
        }
    }
}