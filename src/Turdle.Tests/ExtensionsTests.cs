using System.Collections.Generic;
using System.Linq;
using Turdle.Utils;
using Xunit;

namespace Turdle.Tests;

public class ExtensionsTests
{
    [Fact]
    public void PickRandom_SingleElementList_ReturnsElement()
    {
        var list = new List<int> { 42 };
        var result = list.PickRandom();
        Assert.Equal(42, result);
    }

    [Fact]
    public void PickRandom_CanSelectLastElement()
    {
        var list = new List<int> { 1, 2, 3 };
        var foundLast = false;
        for (var i = 0; i < 100 && !foundLast; i++)
        {
            foundLast = list.PickRandom() == 3;
        }
        Assert.True(foundLast);
    }
}
