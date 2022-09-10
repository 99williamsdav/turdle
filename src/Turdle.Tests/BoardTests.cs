using System.Linq;
using Turdle.Models;
using Xunit;

namespace Turdle.Tests;

public class BoardTests
{
    [Theory]
    [InlineData("ABCDE", "FGHIJ", "JKLMN", HardModeError.AbsentLetterPlayed)]
    [InlineData("ABCDE", "AFGHI", "JKLMN", HardModeError.CorrectLetterMissed)]
    [InlineData("ABCDE", "FGHIA", "JKLMN", HardModeError.PresentLetterMissed)]
    [InlineData("ABCDE", "FGHIA", "JKLMA", HardModeError.PresentLetterPlayedInSamePlace)]
    [InlineData("ABCDE", "AGAIJ", "AKLMA", HardModeError.AbsentLetterPlayed)]
    [InlineData("AACDE", "AGAIJ", "AKLMA")]
    [InlineData("AACDE", "AGAIJ", "AACDE")]
    [InlineData("ABCDE", "JBDCB", "JDDFG", HardModeError.AbsentLetterPlayed, HardModeError.CorrectLetterMissed, HardModeError.PresentLetterPlayedInSamePlace, HardModeError.PresentLetterMissed)]
    [InlineData("ABCDE", "AGABJ", "AKLBA", HardModeError.AbsentLetterPlayed, HardModeError.PresentLetterPlayedInSamePlace)]
    [InlineData("LUPUS", "BLUSH", "LUSTY")]
    [InlineData("ABBBB", "CAACC", "DDDDA")]
    [InlineData("ABBBB", "CAACC", "DDDAA", HardModeError.AbsentLetterPlayed)]
    [InlineData("AABBB", "ACACC", "ADDDD", HardModeError.PresentLetterMissed)]
    [InlineData("AABBB", "CCAAC", "DDDDA", HardModeError.PresentLetterMissed)]
    [InlineData("AABBB", "AACCC", "ADDDA", HardModeError.CorrectLetterMissed)]
    [InlineData("AABBB", "AACCC", "ADDDD", HardModeError.CorrectLetterMissed)]
    public void TestErrors(string answer, string guess1, string guess2, params HardModeError[] expectedErrors)
    {
        var board = new Board();
        var errors = board.AddRow(guess1, answer, 1, 0, 1, null);
        Assert.Empty(errors);
        
        errors = board.AddRow(guess2, answer, 1, 0, 1, null);
        
        Assert.Equal(expectedErrors.Length, errors.Count);
        foreach (var expectedErrorGrp in expectedErrors.GroupBy(x => x))
        {
            var actualErrorCount = errors.Count(x => x.Error == expectedErrorGrp.Key);
            Assert.Equal(expectedErrorGrp.Count(), actualErrorCount);
        }
    }
    
    [Theory]
    [InlineData("LUPUS", "SOLVE", "BLUSH", "LUSTY")]
    [InlineData("ABBBB", "CAACC", "DDDAD", "EEEEA")]
    [InlineData("ABBBB", "CAACC", "DDDAD", "AEEEA", HardModeError.AbsentLetterPlayed)]
    [InlineData("AABBB", "ACACC", "ADDAD", "AEEEA")]
    public void TestThirdGuessErrors(string answer, string guess1, string guess2, string guess3, params HardModeError[] expectedErrors)
    {
        var board = new Board();
        var errors = board.AddRow(guess1, answer, 1, 0, 1, null);
        Assert.Empty(errors);
        
        board.AddRow(guess2, answer, 1, 0, 1, null);
        errors = board.AddRow(guess3, answer, 1, 0, 1, null);
        
        Assert.Equal(expectedErrors.Length, errors.Count);
        foreach (var expectedErrorGrp in expectedErrors.GroupBy(x => x))
        {
            var actualErrorCount = errors.Count(x => x.Error == expectedErrorGrp.Key);
            Assert.Equal(expectedErrorGrp.Count(), actualErrorCount);
        }
    }
}