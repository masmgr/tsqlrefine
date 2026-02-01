using TsqlRefine.Formatting.Helpers;
using Xunit;

namespace TsqlRefine.Formatting.Tests.Helpers;

public class LineEndingHelpersTests
{
    #region DetectLineEnding

    [Fact]
    public void DetectLineEnding_Crlf_ReturnsCrlf()
    {
        var input = "line1\r\nline2\r\nline3";
        var result = LineEndingHelpers.DetectLineEnding(input);
        Assert.Equal("\r\n", result);
    }

    [Fact]
    public void DetectLineEnding_Lf_ReturnsLf()
    {
        var input = "line1\nline2\nline3";
        var result = LineEndingHelpers.DetectLineEnding(input);
        Assert.Equal("\n", result);
    }

    [Fact]
    public void DetectLineEnding_MixedCrlfAndLf_CrlfTakesPrecedence()
    {
        // CRLF takes precedence even when mixed with LF
        var input = "line1\r\nline2\nline3";
        var result = LineEndingHelpers.DetectLineEnding(input);
        Assert.Equal("\r\n", result);
    }

    [Fact]
    public void DetectLineEnding_NoLineEndings_ReturnsDefault()
    {
        var input = "single line without newline";
        var result = LineEndingHelpers.DetectLineEnding(input);
        Assert.Equal("\n", result); // default
    }

    [Fact]
    public void DetectLineEnding_NoLineEndings_ReturnsCustomDefault()
    {
        var input = "single line";
        var result = LineEndingHelpers.DetectLineEnding(input, "\r\n");
        Assert.Equal("\r\n", result);
    }

    [Fact]
    public void DetectLineEnding_EmptyString_ReturnsDefault()
    {
        var result = LineEndingHelpers.DetectLineEnding("");
        Assert.Equal("\n", result);
    }

    [Fact]
    public void DetectLineEnding_OnlyCrlf_ReturnsCrlf()
    {
        var input = "\r\n";
        var result = LineEndingHelpers.DetectLineEnding(input);
        Assert.Equal("\r\n", result);
    }

    [Fact]
    public void DetectLineEnding_OnlyLf_ReturnsLf()
    {
        var input = "\n";
        var result = LineEndingHelpers.DetectLineEnding(input);
        Assert.Equal("\n", result);
    }

    #endregion

    #region SplitByLineEnding

    [Fact]
    public void SplitByLineEnding_Crlf_SplitsCorrectly()
    {
        var input = "line1\r\nline2\r\nline3";
        var result = LineEndingHelpers.SplitByLineEnding(input, "\r\n");
        Assert.Equal(["line1", "line2", "line3"], result);
    }

    [Fact]
    public void SplitByLineEnding_Lf_SplitsCorrectly()
    {
        var input = "line1\nline2\nline3";
        var result = LineEndingHelpers.SplitByLineEnding(input, "\n");
        Assert.Equal(["line1", "line2", "line3"], result);
    }

    [Fact]
    public void SplitByLineEnding_EmptyString_ReturnsSingleEmptyElement()
    {
        var result = LineEndingHelpers.SplitByLineEnding("", "\n");
        Assert.Single(result);
        Assert.Equal("", result[0]);
    }

    [Fact]
    public void SplitByLineEnding_NoLineEndings_ReturnsSingleElement()
    {
        var input = "single line";
        var result = LineEndingHelpers.SplitByLineEnding(input, "\n");
        Assert.Single(result);
        Assert.Equal("single line", result[0]);
    }

    [Fact]
    public void SplitByLineEnding_TrailingLineEnding_IncludesEmptyLastElement()
    {
        var input = "line1\nline2\n";
        var result = LineEndingHelpers.SplitByLineEnding(input, "\n");
        Assert.Equal(["line1", "line2", ""], result);
    }

    [Fact]
    public void SplitByLineEnding_MultipleConsecutiveLineEndings_PreservesEmptyLines()
    {
        var input = "line1\n\n\nline4";
        var result = LineEndingHelpers.SplitByLineEnding(input, "\n");
        Assert.Equal(["line1", "", "", "line4"], result);
    }

    [Fact]
    public void SplitByLineEnding_Crlf_PreservesEmptyLines()
    {
        var input = "line1\r\n\r\nline3";
        var result = LineEndingHelpers.SplitByLineEnding(input, "\r\n");
        Assert.Equal(["line1", "", "line3"], result);
    }

    #endregion
}
