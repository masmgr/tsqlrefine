using TsqlRefine.Formatting.Helpers.Whitespace;
using Xunit;

namespace TsqlRefine.Formatting.Tests.Helpers;

public class BlankLineNormalizerTests
{
    // Use LF explicitly for predictable test behavior, disable other normalizer features
    private static FormattingOptions MakeOptions(
        int maxConsecutiveBlankLines = 0,
        bool trimLeadingBlankLines = false)
    {
        return new FormattingOptions
        {
            LineEnding = LineEnding.Lf,
            InsertFinalNewline = false,
            MaxConsecutiveBlankLines = maxConsecutiveBlankLines,
            TrimLeadingBlankLines = trimLeadingBlankLines
        };
    }

    #region Null / Empty Input

    [Fact]
    public void Normalize_NullInput_ReturnsNull()
    {
        var options = MakeOptions(maxConsecutiveBlankLines: 1);
        var result = BlankLineNormalizer.Normalize(null!, options);
        Assert.Null(result);
    }

    [Fact]
    public void Normalize_EmptyInput_ReturnsEmpty()
    {
        var options = MakeOptions(maxConsecutiveBlankLines: 1);
        var result = BlankLineNormalizer.Normalize("", options);
        Assert.Equal("", result);
    }

    #endregion

    #region Disabled (No-op)

    [Fact]
    public void Normalize_BothDisabled_ReturnsUnchanged()
    {
        var options = MakeOptions(maxConsecutiveBlankLines: 0, trimLeadingBlankLines: false);
        var input = "\n\n\nSELECT 1\n\n\n\nFROM t\n";
        var result = BlankLineNormalizer.Normalize(input, options);
        Assert.Equal(input, result);
    }

    #endregion

    #region MaxConsecutiveBlankLines

    [Fact]
    public void Normalize_MaxBlank1_ThreeConsecutiveBlanks_CollapsesToOne()
    {
        var options = MakeOptions(maxConsecutiveBlankLines: 1);
        var input = "SELECT 1\n\n\n\nFROM t";
        var result = BlankLineNormalizer.Normalize(input, options);
        Assert.Equal("SELECT 1\n\nFROM t", result);
    }

    [Fact]
    public void Normalize_MaxBlank2_FourConsecutiveBlanks_CollapsesToTwo()
    {
        var options = MakeOptions(maxConsecutiveBlankLines: 2);
        var input = "SELECT 1\n\n\n\n\nFROM t";
        var result = BlankLineNormalizer.Normalize(input, options);
        Assert.Equal("SELECT 1\n\n\nFROM t", result);
    }

    [Fact]
    public void Normalize_MaxBlank0_NoChange()
    {
        var options = MakeOptions(maxConsecutiveBlankLines: 0);
        var input = "SELECT 1\n\n\n\nFROM t";
        var result = BlankLineNormalizer.Normalize(input, options);
        Assert.Equal(input, result);
    }

    [Fact]
    public void Normalize_MaxBlank1_ExactlyOneBlanksUnchanged()
    {
        var options = MakeOptions(maxConsecutiveBlankLines: 1);
        var input = "SELECT 1\n\nFROM t";
        var result = BlankLineNormalizer.Normalize(input, options);
        Assert.Equal("SELECT 1\n\nFROM t", result);
    }

    [Fact]
    public void Normalize_MaxBlank1_MultipleSections_CollapsesEach()
    {
        var options = MakeOptions(maxConsecutiveBlankLines: 1);
        var input = "A\n\n\n\nB\n\n\nC";
        var result = BlankLineNormalizer.Normalize(input, options);
        Assert.Equal("A\n\nB\n\nC", result);
    }

    [Fact]
    public void Normalize_MaxBlank1_WhitespaceOnlyBlanks_TreatedAsBlank()
    {
        var options = MakeOptions(maxConsecutiveBlankLines: 1);
        var input = "SELECT 1\n   \n   \n   \nFROM t";
        var result = BlankLineNormalizer.Normalize(input, options);
        Assert.Equal("SELECT 1\n   \nFROM t", result);
    }

    #endregion

    #region TrimLeadingBlankLines

    [Fact]
    public void Normalize_TrimLeading_RemovesLeadingBlanks()
    {
        var options = MakeOptions(trimLeadingBlankLines: true);
        var input = "\n\n\nSELECT 1";
        var result = BlankLineNormalizer.Normalize(input, options);
        Assert.Equal("SELECT 1", result);
    }

    [Fact]
    public void Normalize_TrimLeading_RemovesWhitespaceOnlyLeadingLines()
    {
        var options = MakeOptions(trimLeadingBlankLines: true);
        var input = "   \n  \nSELECT 1";
        var result = BlankLineNormalizer.Normalize(input, options);
        Assert.Equal("SELECT 1", result);
    }

    [Fact]
    public void Normalize_TrimLeadingFalse_PreservesLeadingBlanks()
    {
        var options = MakeOptions(trimLeadingBlankLines: false);
        var input = "\n\nSELECT 1";
        var result = BlankLineNormalizer.Normalize(input, options);
        Assert.Equal("\n\nSELECT 1", result);
    }

    [Fact]
    public void Normalize_TrimLeading_NoLeadingBlanks_Unchanged()
    {
        var options = MakeOptions(trimLeadingBlankLines: true);
        var input = "SELECT 1\nFROM t";
        var result = BlankLineNormalizer.Normalize(input, options);
        Assert.Equal("SELECT 1\nFROM t", result);
    }

    #endregion

    #region Combined Options

    [Fact]
    public void Normalize_BothOptions_TrimsLeadingAndCollapsesConsecutive()
    {
        var options = MakeOptions(maxConsecutiveBlankLines: 1, trimLeadingBlankLines: true);
        var input = "\n\n\nSELECT 1\n\n\n\nFROM t";
        var result = BlankLineNormalizer.Normalize(input, options);
        Assert.Equal("SELECT 1\n\nFROM t", result);
    }

    #endregion

    #region Protected Regions - Block Comments

    [Fact]
    public void Normalize_BlankLinesInsideBlockComment_Preserved()
    {
        var options = MakeOptions(maxConsecutiveBlankLines: 1);
        var input = "/*\n\n\n\n*/\nSELECT 1";
        var result = BlankLineNormalizer.Normalize(input, options);
        // All blank lines inside the block comment should be preserved
        Assert.Equal("/*\n\n\n\n*/\nSELECT 1", result);
    }

    [Fact]
    public void Normalize_BlockCommentSpanningLines_PreservesAll()
    {
        var options = MakeOptions(maxConsecutiveBlankLines: 1);
        var input = "SELECT 1\n/* start\n\n\nend */\nFROM t";
        var result = BlankLineNormalizer.Normalize(input, options);
        Assert.Equal("SELECT 1\n/* start\n\n\nend */\nFROM t", result);
    }

    #endregion

    #region Protected Regions - String Literals

    [Fact]
    public void Normalize_BlankLinesInsideStringLiteral_Preserved()
    {
        var options = MakeOptions(maxConsecutiveBlankLines: 1);
        var input = "SELECT '\n\n\n\n'\nFROM t";
        var result = BlankLineNormalizer.Normalize(input, options);
        // Blank lines inside the string literal should be preserved
        Assert.Equal("SELECT '\n\n\n\n'\nFROM t", result);
    }

    #endregion

    #region Single Line / No Blanks

    [Fact]
    public void Normalize_SingleLine_NoBlanks_Unchanged()
    {
        var options = MakeOptions(maxConsecutiveBlankLines: 1, trimLeadingBlankLines: true);
        var input = "SELECT 1 FROM t";
        var result = BlankLineNormalizer.Normalize(input, options);
        Assert.Equal("SELECT 1 FROM t", result);
    }

    [Fact]
    public void Normalize_MultiLineNoBlanks_Unchanged()
    {
        var options = MakeOptions(maxConsecutiveBlankLines: 1);
        var input = "SELECT 1\nFROM t\nWHERE 1=1";
        var result = BlankLineNormalizer.Normalize(input, options);
        Assert.Equal("SELECT 1\nFROM t\nWHERE 1=1", result);
    }

    #endregion

    #region Final Newline Preservation

    [Fact]
    public void Normalize_FinalNewline_Preserved()
    {
        var options = MakeOptions(maxConsecutiveBlankLines: 1);
        var input = "SELECT 1\n\n\n\nFROM t\n";
        var result = BlankLineNormalizer.Normalize(input, options);
        Assert.Equal("SELECT 1\n\nFROM t\n", result);
    }

    [Fact]
    public void Normalize_FinalNewline_NotTreatedAsBlank()
    {
        var options = MakeOptions(maxConsecutiveBlankLines: 1);
        var input = "SELECT 1\nFROM t\n";
        var result = BlankLineNormalizer.Normalize(input, options);
        Assert.Equal("SELECT 1\nFROM t\n", result);
    }

    #endregion

    #region CRLF Line Endings

    [Fact]
    public void Normalize_CRLF_CollapsesCorrectly()
    {
        var options = new FormattingOptions
        {
            InsertFinalNewline = false,
            MaxConsecutiveBlankLines = 1,
            TrimLeadingBlankLines = false
        };
        var input = "SELECT 1\r\n\r\n\r\n\r\nFROM t";
        var result = BlankLineNormalizer.Normalize(input, options);
        Assert.Equal("SELECT 1\r\n\r\nFROM t", result);
    }

    [Fact]
    public void Normalize_CRLF_TrimLeading_Works()
    {
        var options = new FormattingOptions
        {
            InsertFinalNewline = false,
            MaxConsecutiveBlankLines = 0,
            TrimLeadingBlankLines = true
        };
        var input = "\r\n\r\nSELECT 1";
        var result = BlankLineNormalizer.Normalize(input, options);
        Assert.Equal("SELECT 1", result);
    }

    #endregion
}
