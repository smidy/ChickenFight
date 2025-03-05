using Microsoft.Extensions.AI;

namespace GameServer.Infrastructure.Tests;

[TestClass]
public sealed class Test1
{
    [TestMethod]
    public async Task TestMethod1()
    {
        List<ChatMessage> chatHistory = new();

        var llm = new LLMService();
        var result = await llm.Chat(chatHistory, "Hello what are you?");
        Assert.IsNotNull(result);
    }
}
