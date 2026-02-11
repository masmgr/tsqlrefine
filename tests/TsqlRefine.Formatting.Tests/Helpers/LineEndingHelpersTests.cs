using TsqlRefine.Formatting.Helpers.Whitespace;
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

    #region TransformLines

    [Fact]
    public void TransformLines_Lf_PreservesLineEndingAndTransformsEachLine()
    {
        var input = "line1\nline2";
        var result = LineEndingHelpers.TransformLines(input, (line, _) => $"[{line}]");
        Assert.Equal("[line1]\n[line2]", result);
    }

    [Fact]
    public void TransformLines_Crlf_PreservesLineEndingAndPassesLineIndex()
    {
        var input = "line1\r\nline2\r\nline3";
        var result = LineEndingHelpers.TransformLines(input, (line, index) => $"{index}:{line}");
        Assert.Equal("0:line1\r\n1:line2\r\n2:line3", result);
    }

    [Fact]
    public void TransformLines_NullTransformer_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            LineEndingHelpers.TransformLines("line1", transformLine: null!));
    }

    #endregion

    #region StripStandaloneCr

    [Fact]
    public void StripStandaloneCr_NullInput_ReturnsNull()
    {
        var result = LineEndingHelpers.StripStandaloneCr(null!);
        Assert.Null(result);
    }

    [Fact]
    public void StripStandaloneCr_EmptyString_ReturnsEmpty()
    {
        var result = LineEndingHelpers.StripStandaloneCr("");
        Assert.Equal("", result);
    }

    [Fact]
    public void StripStandaloneCr_NoCr_ReturnsUnchanged()
    {
        var input = "SELECT id\nFROM users";
        var result = LineEndingHelpers.StripStandaloneCr(input);
        Assert.Equal(input, result);
    }

    [Fact]
    public void StripStandaloneCr_CrlfOnly_PreservesCrlf()
    {
        var input = "SELECT id\r\nFROM users\r\nWHERE 1=1";
        var result = LineEndingHelpers.StripStandaloneCr(input);
        Assert.Equal(input, result);
    }

    [Fact]
    public void StripStandaloneCr_StandaloneCr_Removed()
    {
        var input = "SELECT id\rFROM users";
        var result = LineEndingHelpers.StripStandaloneCr(input);
        Assert.Equal("SELECT idFROM users", result);
    }

    [Fact]
    public void StripStandaloneCr_MultipleStandaloneCr_AllRemoved()
    {
        var input = "SELECT\rid\rFROM\rusers";
        var result = LineEndingHelpers.StripStandaloneCr(input);
        Assert.Equal("SELECTidFROMusers", result);
    }

    [Fact]
    public void StripStandaloneCr_MixedCrlfAndStandaloneCr_OnlyStandaloneRemoved()
    {
        var input = "SELECT id\r\nFROM users\rWHERE 1=1";
        var result = LineEndingHelpers.StripStandaloneCr(input);
        Assert.Equal("SELECT id\r\nFROM usersWHERE 1=1", result);
    }

    [Fact]
    public void StripStandaloneCr_CrAtEndOfString_Removed()
    {
        var input = "SELECT id\r";
        var result = LineEndingHelpers.StripStandaloneCr(input);
        Assert.Equal("SELECT id", result);
    }

    [Fact]
    public void StripStandaloneCr_ConsecutiveCr_AllRemoved()
    {
        var input = "SELECT\r\rid";
        var result = LineEndingHelpers.StripStandaloneCr(input);
        Assert.Equal("SELECTid", result);
    }

    [Fact]
    public void StripStandaloneCr_CrBeforeCrlf_StandaloneRemovedCrlfPreserved()
    {
        // \r followed by \r\n: first \r is standalone (next char is \r, not \n), remove it
        var input = "SELECT\r\r\nFROM";
        var result = LineEndingHelpers.StripStandaloneCr(input);
        Assert.Equal("SELECT\r\nFROM", result);
    }

    #endregion
}
