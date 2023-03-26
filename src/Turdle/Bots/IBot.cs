using Turdle.Models;

namespace Turdle.Bots
{
    public interface IBot
    {
        public Task<string> SelectOpeningWord(int wordLength);
        public Task<string> SelectWord(int wordLength, Board board, string? correctAnswer);
    }
}
