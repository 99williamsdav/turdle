using Turdle.Models;
using Turdle.Utils;

namespace Turdle.Bots
{
    public class DumbBot : IBot
    {
        private readonly WordService _wordService;

        public DumbBot(WordService wordService)
        {
            _wordService = wordService;
        }

        public Task<string> SelectOpeningWord(int wordLength)
        {
            var options = _wordService.GetReasonableBotWords(wordLength);
            return Task.FromResult(options.PickRandom());
        }

        public Task<(string Word, double Speed)> SelectWord(int wordLength, Board board, string? correctAnswer)
        {
            var options =
                _wordService.GetPossibleValidGuesses(board.CorrectLetters, board.PresentLetters, board.AbsentLetters, board.PresentLetterCounts, wordLength);

            if (correctAnswer != null && !options.Contains(correctAnswer))
                options = options.Append(correctAnswer).ToArray();

            return Task.FromResult((options.PickRandom(), 0.5));
        }

        public Task<string?> GetSmackTalk()
        {
            return Task.FromResult<string?>(null);
        }

        public Task Init() => Task.CompletedTask;
    }
}
