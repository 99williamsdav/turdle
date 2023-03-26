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

        public ChatGptPersonalityBot(string personality, ChatGptService chatGptService, WordService wordService)
        {
            _chatGptService = chatGptService;
            _personality = personality;
            _wordService = wordService;
        }

        public async Task<string> SelectOpeningWord(int wordLength)
        {
            var openingWords = await _chatGptService.GetOpeningWordsByPersonality(_personality, wordLength);
            var guess = openingWords.PickRandom();
            return guess;
        }

        public async Task<string> SelectWord(int wordLength, Board board, string? correctAnswer)
        {
            var options =
                _wordService.GetPossibleValidGuesses(board.CorrectLetters, board.PresentLetters, board.AbsentLetters, board.PresentLetterCounts, wordLength);

            if (correctAnswer != null && !options.Contains(correctAnswer))
                options = options.Append(correctAnswer).ToArray();

            var guess = await _chatGptService.GetPersonalityWordChoice(_personality, options);
            return guess;
        }
    }
}
