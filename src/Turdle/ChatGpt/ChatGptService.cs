using Turdle.Utils;

namespace Turdle.ChatGpt
{
    public class ChatGptService
    {
        private readonly ILogger<ChatGptService> _logger;
        private readonly ChatGptClient _chatGptClient;

        public ChatGptService(ChatGptClient chatGptClient, ILogger<ChatGptService> logger)
        {
            _chatGptClient = chatGptClient;
            _logger = logger;
        }

        public async Task<string[]> GetOpeningWordsByPersonality(string personality, int length)
        {
            var prompt = $"give me 10 {length}-letter words that {personality} might play in scrabble, " +
                $"as a comma-separated list with no additional information, ordered by likelihood";
            var response = await _chatGptClient.GetChatCompletion(prompt);
            var suggestions = response.Replace(" ", "").Split(",");
            var validSuggestions = suggestions.Where(x => x.Length == length).Select(x => x.ToUpper()).ToArray();
            return validSuggestions;
        }

        public async Task<string> GetPersonalityWordChoice(string personality, IList<string> options)
        {
            if (options.Count == 1)
                return options.Single();

            var csOptions = string.Join(",", options);
            var prompt = $"Which of these comma-separated words would {personality} be most likely to play in scrabble? " +
                $"Please answer with one single word. It absolutely has to be a word in the list\n\n{csOptions}";
            const int maxAttempts = 3;
            for (int i = 0; i < maxAttempts; i++)
            {
                var choice = await _chatGptClient.GetChatCompletion(prompt);
                if (options.Contains(choice.ToUpper()))
                {
                    return choice.ToUpper();
                }

                _logger.LogWarning($"ChatGPT returned word selection ({choice}) not in option list.");
            }

            return options.PickRandom();
        }
    }
}
