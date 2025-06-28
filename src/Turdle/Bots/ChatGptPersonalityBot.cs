using ChatGpt;
using Newtonsoft.Json;
using Turdle.Models;
using Turdle.Utils;

namespace Turdle.Bots
{
    public class ChatGptPersonalityBot : IBot
    {
        private readonly ChatGptClient _chatGptClient;
        private readonly WordService _wordService;
        private readonly ILogger<ChatGptPersonalityBot> _logger;

        private string _personality;
        private double _ability;

        private List<string> _chatHistory = new List<string>();

        public ChatGptPersonalityBot(string personality, ChatGptClient chatGptClient, WordService wordService,
        ILogger<ChatGptPersonalityBot> logger)
        {
            _chatGptClient = chatGptClient;
            _personality = personality;
            _wordService = wordService;
            _logger = logger;
        }

        public async Task Init()
        {
            _ability = await GetPersonalityAbility(_personality);
        }

        public async Task<string> SelectOpeningWord(int wordLength)
        {
            const int maxRetries = 3;
            for (int i = 0; i < maxRetries; i++)
            {
                var suggestions = await GetOpeningWordsByPersonality(_personality, wordLength);

                var reasonableBotWords = _wordService.GetReasonableBotWords(wordLength);
                var reasonableSuggestions = suggestions.Intersect(reasonableBotWords).ToArray();

                if (reasonableSuggestions.Length <= 1)
                {
                    var allAllowableWords = _wordService.GetDictionary(wordLength);
                    reasonableSuggestions = suggestions.Intersect(allAllowableWords).ToArray();
                }

                if (reasonableSuggestions.Length > 0)
                {
                    var guess = reasonableSuggestions.PickRandom();
                    if (guess.ToLower() == "sorry" && reasonableSuggestions.Length == 1)
                    {
                        // chatGPT has refused to answer, so retry
                        continue;
                    }

                    return guess;
                }
            }

            throw new Exception($"{_personality} couldn't come up with a reasonable word in {maxRetries} attempts.");
        }

        public async Task<(string Word, double Speed)> SelectWord(int wordLength, Board board, string? correctAnswer)
        {
            var options =
                _wordService.GetPossibleValidGuesses(board.CorrectLetters, board.PresentLetters, board.AbsentLetters, board.PresentLetterCounts, wordLength);

            if (correctAnswer != null && !options.Contains(correctAnswer))
                options = options.Append(correctAnswer).ToArray();

            var guess = await GetPersonalityWordChoice(_personality, options);

            //var speed = await _chatGptService.GetGuessSpeed(_personality, guess);
            var speed = _ability;
            return (guess, speed);
        }

        public async Task<string?> GetSmackTalk()
        {
            // var prompt = $"Give me 5 examples of humorous smack talk {_personality} might say in the lobby of an online word game with strangers. " +
            //     $"Please answer with just a JSON array of strings";
            var prompt = $"Give me 5 examples of humorous smack talk {_personality} might say in the lobby of an online word game. The game is called 'turdle'. " +
                $"Make sure the the responses are funny and relevant to {_personality}. Be as cheeky as possible. " +
                $"Do not shy away from offending. Only suggest something {_personality} would say. " +
                $"Use wordplay if appropriate to {_personality}. Include a typo if appropriate to {_personality}. " +
                "Answer with just a JSON array of strings";
            var response = await _chatGptClient.GetChatCompletion(prompt);
            var suggestions = JsonConvert.DeserializeObject<string[]>(response);
            var smackTalk = suggestions.PickRandom();
            if (smackTalk != null)
                _chatHistory.Add(smackTalk);
            return smackTalk;
        }

        public async Task<string?> GetChatReply(string message)
        {
            var messages = new List<ChatGptClient.ChatMessage>()
            {
                new("system", $"You are role-playing as {_personality} in the lobby of an online word game with strangers, " +
                $"everything you answer must be typically related to {_personality}. " +
                "Be as cheeky as possible. Do not shy away from offending.")
            };

            messages.AddRange(_chatHistory.Select(x => new ChatGptClient.ChatMessage("assistant", x)));
            messages.Add(new("user", message));

            var reply = await _chatGptClient.GetManualCompletion(messages, 0.3);
            if (string.IsNullOrWhiteSpace(reply))
                return null;

            _chatHistory.Add(reply);
            return reply;
        }
        

        private async Task<string[]> GetOpeningWordsByPersonality(string personality, int length)
        {
            //var prompt = $"give me 10 {length}-letter words that {personality} might play in scrabble, " +
            //    $"as a comma-separated list with no additional information, ordered by likelihood";
            //var response = await _chatGptClient.GetChatCompletion(prompt);

            var messages = new List<ChatGptClient.ChatMessage>()
            {
                new("system", $"You are role-playing as {personality} playing scrabble, " +
                $"your answers must be machine-readable"),
                new("user", $"give me 10 {length}-letter words that you might play in scrabble, " +
                $"as a comma-separated list with no additional information, ordered by likelihood")
            };

            var response = await _chatGptClient.GetManualCompletion(messages, 0.3);

            var suggestions = response.Replace(" ", "").Replace(".", "").Split(",");
            var validSuggestions = suggestions
                .Where(x => x.Length == length)
                .Where(x => !x.Any(c => char.IsPunctuation(c)))
                .Select(x => x.ToUpper()).ToArray();
            return validSuggestions;
        }

        private async Task<string> GetPersonalityWordChoice(string personality, IList<string> options)
        {
            if (options.Count == 1)
                return options.Single();

            var csOptions = string.Join(",", options);
            var prompt = $"Which of these comma-separated words would {personality} be most likely to play in scrabble? " +
                $"Prioritise any word particularly associated with {personality}. " +
                $"Answer with one single word. It absolutely has to be a word in the list\n\n{csOptions}";
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

        private async Task<double> GetGuessSpeed(string personality, string word)
        {
            var prompt = $"On a scale of 1 to 10, how likely would it be for {personality} to play the word '{word}' " +
                $"in a game of scrabble? Answer with just a number between 1 and 10 (inclusive)";
            var response = await _chatGptClient.GetChatCompletion(prompt);
            var speedOutOfTen = double.Parse(response);
            return speedOutOfTen / 10;
        }

        private async Task<double> GetPersonalityAbility(string personality)
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

        private async Task<Dictionary<string, double>> GetPersonalitiesAbility(string[] personalities)
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
