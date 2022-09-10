using System.Collections.Generic;
using Turdle.Models;
using Xunit;

namespace Turdle.Tests;

public class RowTests
{
    [Theory]
    [InlineData("WRONG", "BLAME", TileStatus.Absent, TileStatus.Absent, TileStatus.Absent, TileStatus.Absent, TileStatus.Absent)]
    [InlineData("SLIME", "BLAME", TileStatus.Absent, TileStatus.Correct, TileStatus.Absent, TileStatus.Correct, TileStatus.Correct)]
    [InlineData("FALSE", "BLAME", TileStatus.Absent, TileStatus.Present, TileStatus.Present, TileStatus.Absent, TileStatus.Correct)]
    [InlineData("ALARM", "BLAME", TileStatus.Absent, TileStatus.Correct, TileStatus.Correct, TileStatus.Absent, TileStatus.Present)]
    [InlineData("BLAME", "BLAME", TileStatus.Correct, TileStatus.Correct, TileStatus.Correct, TileStatus.Correct, TileStatus.Correct)]
    [InlineData("LIMES", "SLIME", TileStatus.Present, TileStatus.Present, TileStatus.Present, TileStatus.Present, TileStatus.Present)]
    [InlineData("AMAZE", "BLAST", TileStatus.Absent, TileStatus.Absent, TileStatus.Correct, TileStatus.Absent, TileStatus.Absent)]
    [InlineData("ALTAR", "BLAST", TileStatus.Present, TileStatus.Correct, TileStatus.Present, TileStatus.Absent, TileStatus.Absent)]
    public void InitTileStatus(string guess, string answer, params TileStatus[] statuses)
    {
        var row = new Board.Row(guess, answer, 1, 1, new List<Board.PointAdjustment>());
        for (var i = 0; i < 5; i++)
        {
            var letter = row.Tiles[i].Letter;
            Assert.Equal(statuses[i], row.Tiles[i].Status);
        }
    }

    [Theory]
    [InlineData(3, 1, 1)]
    [InlineData(3, 2, 0.5)]
    [InlineData(3, 3, 0)]
    [InlineData(2, 1, 1)]
    [InlineData(2, 2, 0)]
    [InlineData(5, 1, 1)]
    [InlineData(5, 2, 0.75)]
    [InlineData(5, 3, 0.5)]
    [InlineData(5, 4, 0.25)]
    [InlineData(5, 5, 0)]
    public void TestPointRatio(int playerCount, int order, double expected)
    {
        var ratio = 1d / (playerCount - 1) * (playerCount - order);
        Assert.Equal(expected, ratio);
    }
}