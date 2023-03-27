using Turdle.ChatGpt;

namespace Turdle.Bots
{
    public class BotFactory
    {
        private readonly ChatGptService _chatGptService;
        private readonly WordService _wordService;

        public BotFactory(ChatGptService chatGptService, WordService wordService)
        {
            _chatGptService = chatGptService;
            _wordService = wordService;
        }

        public IBot CreateBot(BotParams botParams)
        {
            switch (botParams.BotType)
            {
                case BotType.Dumb:
                    return new DumbBot(_wordService);
                case BotType.ChatGptPersonality:
                    return new ChatGptPersonalityBot(botParams.Personality, _chatGptService, _wordService);
                default:
                    throw new ArgumentException($"Unknown bot type {botParams.BotType}");
            }
        }
    }

    public enum BotType
    {
        Dumb,
        ChatGptPersonality
    }

    public record BotParams(BotType BotType, string Personality);
}