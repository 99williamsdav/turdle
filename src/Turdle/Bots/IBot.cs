using Turdle.Models;

namespace Turdle.Bots
{
    public interface IBot
    {
        public Task Init();
        public Task<string> SelectOpeningWord(int wordLength);
        public Task<(string Word, double Speed)> SelectWord(int wordLength, Board board, string? correctAnswer);
        Task<string?> GetSmackTalk();
    }
}
