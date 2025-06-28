using ChatGpt;

namespace Turdle.Bots
{
    public class BotFactory
    {
        private readonly ChatGptClient _chatGptClient;
        private readonly WordService _wordService;
        private readonly ILogger<ChatGptPersonalityBot> _logger;

        public BotFactory(WordService wordService, ChatGptClient chatGptClient, ILogger<ChatGptPersonalityBot> logger)
        {
            _wordService = wordService;
            _chatGptClient = chatGptClient;
            _logger = logger;
        }

        public IBot CreateBot(BotParams botParams)
        {
            switch (botParams.BotType)
            {
                case BotType.Dumb:
                    return new DumbBot(_wordService);
                case BotType.ChatGptPersonality:
                    return new ChatGptPersonalityBot(botParams.Personality, _chatGptClient, _wordService, _logger);
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