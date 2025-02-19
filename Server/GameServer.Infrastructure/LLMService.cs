﻿using Microsoft.Extensions.AI;

namespace GameServer.Infrastructure
{
    public class LLMService
    {
        private readonly IChatClient _chatClient;
        
        public LLMService(IChatClient? chatClient = null)
        {
            _chatClient = chatClient ?? new OllamaChatClient(new Uri("http://127.0.0.1:11434/"), "deepseek-r1");
        }

        public async Task<List<ChatMessage>> Chat(List<ChatMessage> chatHistory, string userPrompt)
        {
            chatHistory.Add(new ChatMessage(ChatRole.User, userPrompt));

            var response = "";
            await foreach (var item in _chatClient.GetStreamingResponseAsync(chatHistory))
            {
                Console.Write(item.Text);
                response += item.Text;
            }
            chatHistory.Add(new ChatMessage(ChatRole.Assistant, response));
            return chatHistory;
        }
    }
}
