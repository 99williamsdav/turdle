using Newtonsoft.Json;
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
            var suggestions = response.Replace(" ", "").Replace(".", "").Split(",");
            var validSuggestions = suggestions
                .Where(x => x.Length == length)
                .Where(x => !x.Any(c => char.IsPunctuation(c)))
                .Select(x => x.ToUpper()).ToArray();
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
                var sanitised = choice.Replace(".", "").ToUpper();
                if (options.Contains(sanitised))
                {
                    return sanitised;
                }

                _logger.LogWarning($"ChatGPT returned word selection ({sanitised}) not in option list.");
            }

            return options.PickRandom();
        }

        public async Task<double> GetGuessSpeed(string personality, string word)
        {
            var prompt = $"On a scale of 1 to 10, how likely would it be for {personality} to play the word '{word}' " +
                $"in a game of scrabble? Please answer with just a number between 1 and 10 (inclusive)";
            var response = await _chatGptClient.GetChatCompletion(prompt);
            var speedOutOfTen = double.Parse(response);
            return speedOutOfTen / 10;
        }

        public async Task<string> GetPersonalitySmackTalk(string personality)
        {
            var prompt = $"Give me 5 examples of humorous smack talk {personality} might say in the lobby of an online game with strangers. " +
                $"Please answer with just a JSON array of strings";
            var response = await _chatGptClient.GetChatCompletion(prompt);
            var suggestions = JsonConvert.DeserializeObject<string[]>(response);
            return suggestions.PickRandom();
        }

        public async Task<double> GetPersonalityAbility(string personality)
        {
            var abilities = await GetPersonalitiesAbility(new[] { "a toddler", personality, "shakespeare" });
            var abilityOutOfTen = abilities[personality.ToLower()];

            //var prompt = $"On a scale of 1 to 10, how good might {personality} be at wordle? " +
            //    $"Answer with just a number between 1 and 10 (inclusive)";
            //var prompt = $"Hypothetically how good might {personality} be at a word game? " +
            //    $"Answer with just a number between 1 and 10 where a toddler would be 1 and shakespeare would be 10";
            //var response = await _chatGptClient.GetChatCompletion(prompt);
            //var abilityOutOfTen = double.Parse(response);
            if (abilityOutOfTen > 10)
                _logger.LogWarning($"ChatGPT returned wordle ability ({abilityOutOfTen}) over 10.");
            _logger.LogInformation($"ChatGPT - {personality} scrabble ability = {abilityOutOfTen}/10");
            return abilityOutOfTen / 10;
        }

        public async Task<Dictionary<string, double>> GetPersonalitiesAbility(string[] personalities)
        {
            personalities = personalities.Select(x => x.ToLower()).Distinct().ToArray();
            var csPersonalities = string.Join(",", personalities);
            var prompt = $"Hypothetically how good might this comma-separated list of people be at a word game? " +
                $"Answer with just a JSON array with the properties 'person' and 'ability'. " +
                $"The ability score should be a number between 1 and 10 where a toddler would be 1, shakespeare would be 10 " +
                $"and an average adult would be 5\r\n{csPersonalities}";

            const int maxAttempts = 3;
            for (int i = 0; i < maxAttempts; i++)
            {
                var response = await _chatGptClient.GetChatCompletion(prompt);
                var abilities = JsonConvert.DeserializeObject<PersonalityResponse[]>(response);
                var abilityByPersonality = abilities.ToDictionary(x => x.person.ToLower(), x => x.ability);
                _logger.LogInformation($"ChatGPT - bulk scrabble abilities = {response}");
                if (abilityByPersonality.Count == personalities.Length && personalities.All(abilityByPersonality.ContainsKey))
                    return abilityByPersonality;

                _logger.LogWarning($"ChatGPT didn't return with valid abilities for everyone: {response}");
            }

            return new Dictionary<string, double>();
        }

        private record PersonalityResponse(string person, double ability);
    }
}
