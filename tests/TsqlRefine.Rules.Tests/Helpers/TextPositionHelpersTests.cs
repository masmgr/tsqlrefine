using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Tests.Helpers;

public sealed class TextPositionHelpersTests
{
    [Fact]
    public void AdvancePosition_EmptyText_ReturnsStart()
    {
        var start = new Position(3, 7);

        Assert.Equal(start, TextPositionHelpers.AdvancePosition(start, ""));
        Assert.Equal(start, TextPositionHelpers.AdvancePosition(start, null));
    }

    [Fact]
    public void AdvancePosition_SingleLineText_AdvancesCharacterOnly()
    {
        var result = TextPositionHelpers.AdvancePosition(new Position(2, 4), "SELECT");

        Assert.Equal(new Position(2, 10), result);
    }

    [Theory]
    [InlineData("a\nbc", 1, 2)]
    [InlineData("a\r\nbc", 1, 2)]
    [InlineData("a\rbc", 1, 2)]
    [InlineData("\n", 1, 0)]
    [InlineData("\r\n", 1, 0)]
    [InlineData("line1\nline2\nx", 2, 1)]
    [InlineData("'multi\nline\nstring'", 2, 7)]
    public void AdvancePosition_MultiLineText_AdvancesLineAndCharacter(
        string text, int expectedLine, int expectedCharacter)
    {
        var result = TextPositionHelpers.AdvancePosition(new Position(5, 9), text);

        Assert.Equal(new Position(5 + expectedLine, expectedCharacter), result);
    }

    [Fact]
    public void AdvancePosition_NullStart_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            TextPositionHelpers.AdvancePosition(null!, "text"));
    }
}
