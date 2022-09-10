using Turdle.Models;
using Xunit;

namespace Turdle.Tests;

public class WordServiceTests
{
    private readonly WordService _wordService;

    public WordServiceTests()
    {
        _wordService = new WordService();
    }

    [Fact]
    public void Go()
    {
        
    }

    [Theory]
    [InlineData("WRONG", "WRING", "OOWOO", "WRONG")]
    public void PossibleValidGuesses(string answer, string guess1, string guess2, params string[] expectedValidGuesses)
    {
        var board = new Board();
        board.AddRow(guess1, answer, 1, 0, 1, null);
        board.AddRow(guess2, answer, 1, 0, 1, null);

        var validGuesses =
            _wordService.GetPossibleValidGuesses(board.CorrectLetters, board.PresentLetters, board.AbsentLetters, board.PresentLetterCounts, 5);

        foreach (var guess in expectedValidGuesses)
        {
            Assert.Contains(guess, validGuesses);
        }
    }

    [Theory]
    [InlineData("WRONG", "WRING", "ABWCO", "WRUNG")]
    public void ImpossibleValidGuesses(string answer, string guess1, string guess2, params string[] expectedInvalidGuesses)
    {
        var board = new Board();
        board.AddRow(guess1, answer, 1, 0, 1, null);
        board.AddRow(guess2, answer, 1, 0, 1, null);

        var validGuesses =
            _wordService.GetPossibleValidGuesses(board.CorrectLetters, board.PresentLetters, board.AbsentLetters, board.PresentLetterCounts, 5);
        Assert.NotEmpty(validGuesses);

        foreach (var guess in expectedInvalidGuesses)
        {
            Assert.DoesNotContain(guess, validGuesses);
        }
    }
}