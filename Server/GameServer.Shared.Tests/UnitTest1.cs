using GameServer.Shared.Messages.Connection;

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
            await Task.Delay(3000);
        }
    }
}