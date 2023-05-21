using Turdle.ChatGpt;
using Turdle.Models;
using Turdle.Utils;

namespace Turdle.Bots
{
    public class ChatGptPersonalityBot : IBot
    {
        private readonly ChatGptService _chatGptService;
        private readonly WordService _wordService;

        private string _personality;
        private double _ability;

        private List<string> _chatHistory = new List<string>();

        public ChatGptPersonalityBot(string personality, ChatGptService chatGptService, WordService wordService)
        {
            _chatGptService = chatGptService;
            _personality = personality;
            _wordService = wordService;
        }

        public async Task Init()
        {
            _ability = await _chatGptService.GetPersonalityAbility(_personality);
        }

        public async Task<string> SelectOpeningWord(int wordLength)
        {
            const int maxRetries = 3;
            for (int i = 0; i < maxRetries; i++)
            {
                var openingWords = await _chatGptService.GetOpeningWordsByPersonality(_personality, wordLength);
                // is this too limiting?
                var reasonableBotWords = _wordService.GetReasonableBotWords(wordLength);
                openingWords = openingWords.Intersect(reasonableBotWords).ToArray();
                if (openingWords.Length > 0)
                {
                    var guess = openingWords.PickRandom();
                    if (guess.ToLower() == "sorry")
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

            var guess = await _chatGptService.GetPersonalityWordChoice(_personality, options);

            //var speed = await _chatGptService.GetGuessSpeed(_personality, guess);
            var speed = _ability;
            return (guess, speed);
        }

        public async Task<string?> GetSmackTalk()
        {
            var smackTalk = await _chatGptService.GetPersonalitySmackTalk(_personality);
            if (smackTalk != null)
                _chatHistory.Add(smackTalk);
            return smackTalk;
        }

        public async Task<string?> GetChatReply(string message)
        {
            var reply = await _chatGptService.GetChatReply(_personality, _chatHistory, message);
            if (string.IsNullOrWhiteSpace(reply))
                return null;

            _chatHistory.Add(reply);
            return reply;
        }
    }
}
