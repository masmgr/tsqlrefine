using TsqlRefine.Formatting;
using TsqlRefine.Formatting.Helpers;
using Xunit;

namespace TsqlRefine.Formatting.Tests.Helpers;

public class WhitespaceNormalizerTests
{
    // Default options have InsertFinalNewline=true and TrimTrailingWhitespace=true
    // Use LF explicitly for predictable test behavior
    private readonly FormattingOptions _defaultOptions = new() { LineEnding = LineEnding.Lf };

    // Options without final newline for testing specific behaviors
    private readonly FormattingOptions _noFinalNewlineOptions = new() { InsertFinalNewline = false, LineEnding = LineEnding.Lf };

    #region Basic Input Processing

    [Fact]
    public void Normalize_EmptyString_ReturnsEmpty()
    {
        var result = WhitespaceNormalizer.Normalize("", _noFinalNewlineOptions);
        Assert.Equal("", result);
    }

    [Fact]
    public void Normalize_WhitespaceOnly_TrimsToEmpty()
    {
        // With TrimTrailingWhitespace=true (default), whitespace-only line becomes empty
        var options = new FormattingOptions { InsertFinalNewline = false, TrimTrailingWhitespace = true };
        var result = WhitespaceNormalizer.Normalize("   ", options);
        Assert.Equal("", result);
    }

    [Fact]
    public void Normalize_SingleLine_ReturnsWithFinalNewline()
    {
        var input = "SELECT * FROM Users";
        var result = WhitespaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal(input + "\n", result);
    }

    [Fact]
    public void Normalize_SingleLine_NoFinalNewline()
    {
        var input = "SELECT * FROM Users";
        var result = WhitespaceNormalizer.Normalize(input, _noFinalNewlineOptions);
        Assert.Equal(input, result);
    }

    #endregion

    #region Indentation - Spaces

    [Fact]
    public void Normalize_SpacesIndent_PreservesSpaces()
    {
        var options = new FormattingOptions { IndentStyle = IndentStyle.Spaces, IndentSize = 4, InsertFinalNewline = false };
        var input = "    SELECT * FROM Users";
        var result = WhitespaceNormalizer.Normalize(input, options);
        Assert.Equal("    SELECT * FROM Users", result);
    }

    [Fact]
    public void Normalize_TabsToSpaces_ConvertsCorrectly()
    {
        var options = new FormattingOptions { IndentStyle = IndentStyle.Spaces, IndentSize = 4, InsertFinalNewline = false };
        var input = "\tSELECT * FROM Users";
        var result = WhitespaceNormalizer.Normalize(input, options);
        Assert.Equal("    SELECT * FROM Users", result);
    }

    [Fact]
    public void Normalize_MixedIndentToSpaces_ConvertsCorrectly()
    {
        var options = new FormattingOptions { IndentStyle = IndentStyle.Spaces, IndentSize = 4, InsertFinalNewline = false };
        var input = "  \tSELECT * FROM Users"; // 2 spaces + 1 tab = 6 columns
        var result = WhitespaceNormalizer.Normalize(input, options);
        Assert.Equal("      SELECT * FROM Users", result);
    }

    [Theory]
    [InlineData(2, "  SELECT")]
    [InlineData(4, "    SELECT")]
    [InlineData(8, "        SELECT")]
    public void Normalize_SpacesIndentSize_AppliesCorrectly(int indentSize, string expected)
    {
        var options = new FormattingOptions { IndentStyle = IndentStyle.Spaces, IndentSize = indentSize, InsertFinalNewline = false };
        var input = "\tSELECT";
        var result = WhitespaceNormalizer.Normalize(input, options);
        Assert.Equal(expected, result);
    }

    #endregion

    #region Indentation - Tabs

    [Fact]
    public void Normalize_TabsIndent_PreservesTabs()
    {
        var options = new FormattingOptions { IndentStyle = IndentStyle.Tabs, IndentSize = 4, InsertFinalNewline = false };
        var input = "\tSELECT * FROM Users";
        var result = WhitespaceNormalizer.Normalize(input, options);
        Assert.Equal("\tSELECT * FROM Users", result);
    }

    [Fact]
    public void Normalize_SpacesToTabs_ConvertsCorrectly()
    {
        var options = new FormattingOptions { IndentStyle = IndentStyle.Tabs, IndentSize = 4, InsertFinalNewline = false };
        var input = "    SELECT * FROM Users"; // 4 spaces = 1 tab
        var result = WhitespaceNormalizer.Normalize(input, options);
        Assert.Equal("\tSELECT * FROM Users", result);
    }

    [Fact]
    public void Normalize_SpacesToTabs_PartialTab()
    {
        var options = new FormattingOptions { IndentStyle = IndentStyle.Tabs, IndentSize = 4, InsertFinalNewline = false };
        var input = "      SELECT * FROM Users"; // 6 spaces = 1 tab + 2 spaces
        var result = WhitespaceNormalizer.Normalize(input, options);
        Assert.Equal("\t  SELECT * FROM Users", result);
    }

    [Fact]
    public void Normalize_MixedIndentToTabs_ConvertsCorrectly()
    {
        var options = new FormattingOptions { IndentStyle = IndentStyle.Tabs, IndentSize = 4, InsertFinalNewline = false };
        var input = "  \tSELECT * FROM Users"; // 2 spaces + 1 tab (4 spaces) = 6 columns = 1 tab + 2 spaces
        var result = WhitespaceNormalizer.Normalize(input, options);
        Assert.Equal("\t  SELECT * FROM Users", result);
    }

    #endregion

    #region Trailing Whitespace

    [Fact]
    public void Normalize_TrimTrailingWhitespace_Enabled()
    {
        var options = new FormattingOptions { TrimTrailingWhitespace = true, InsertFinalNewline = false };
        var input = "SELECT * FROM Users   ";
        var result = WhitespaceNormalizer.Normalize(input, options);
        Assert.Equal("SELECT * FROM Users", result);
    }

    [Fact]
    public void Normalize_TrimTrailingWhitespace_Disabled()
    {
        var options = new FormattingOptions { TrimTrailingWhitespace = false, InsertFinalNewline = false };
        var input = "SELECT * FROM Users   ";
        var result = WhitespaceNormalizer.Normalize(input, options);
        Assert.Equal("SELECT * FROM Users   ", result);
    }

    [Fact]
    public void Normalize_TrimTrailingWhitespace_MultiLine()
    {
        var options = new FormattingOptions { TrimTrailingWhitespace = true, InsertFinalNewline = false };
        var input = "SELECT *   \nFROM Users   ";
        var result = WhitespaceNormalizer.Normalize(input, options);
        Assert.Equal("SELECT *\nFROM Users", result);
    }

    #endregion

    #region Final Newline

    [Fact]
    public void Normalize_InsertFinalNewline_AddsNewline()
    {
        var options = new FormattingOptions { InsertFinalNewline = true };
        var input = "SELECT * FROM Users";
        var result = WhitespaceNormalizer.Normalize(input, options);
        Assert.EndsWith("\n", result);
    }

    [Fact]
    public void Normalize_InsertFinalNewline_AlreadyHasNewline()
    {
        var options = new FormattingOptions { InsertFinalNewline = true };
        var input = "SELECT * FROM Users\n";
        var result = WhitespaceNormalizer.Normalize(input, options);
        Assert.Equal("SELECT * FROM Users\n", result);
    }

    [Fact]
    public void Normalize_NoFinalNewline_DoesNotAdd()
    {
        var options = new FormattingOptions { InsertFinalNewline = false };
        var input = "SELECT * FROM Users";
        var result = WhitespaceNormalizer.Normalize(input, options);
        Assert.DoesNotContain("\n", result);
    }

    #endregion

    #region Line Endings

    [Fact]
    public void Normalize_LF_PreservesLineBreaks()
    {
        var options = new FormattingOptions { InsertFinalNewline = false };
        var input = "SELECT *\nFROM Users\nWHERE id = 1";
        var result = WhitespaceNormalizer.Normalize(input, options);
        Assert.Contains("\n", result);
        Assert.DoesNotContain("\r", result); // Auto mode detects LF and uses LF
        Assert.Equal(2, result.Count(c => c == '\n'));
    }

    [Fact]
    public void Normalize_CRLF_PreservesCRLF()
    {
        // Auto mode detects CRLF and preserves it
        var options = new FormattingOptions { InsertFinalNewline = false };
        var input = "SELECT *\r\nFROM Users\r\nWHERE id = 1";
        var result = WhitespaceNormalizer.Normalize(input, options);
        Assert.Contains("\r\n", result);
        Assert.Equal(2, CountOccurrences(result, "\r\n"));
    }

    [Fact]
    public void Normalize_CRLF_WithExplicitLf_ConvertsToLF()
    {
        // Explicit LF mode converts CRLF to LF
        var options = new FormattingOptions { InsertFinalNewline = false, LineEnding = LineEnding.Lf };
        var input = "SELECT *\r\nFROM Users\r\nWHERE id = 1";
        var result = WhitespaceNormalizer.Normalize(input, options);
        Assert.DoesNotContain("\r", result);
        Assert.Equal(2, result.Count(c => c == '\n'));
    }

    [Fact]
    public void Normalize_LF_WithExplicitCrLf_ConvertsToCRLF()
    {
        // Explicit CRLF mode converts LF to CRLF
        var options = new FormattingOptions { InsertFinalNewline = false, LineEnding = LineEnding.CrLf };
        var input = "SELECT *\nFROM Users\nWHERE id = 1";
        var result = WhitespaceNormalizer.Normalize(input, options);
        Assert.Contains("\r\n", result);
        Assert.Equal(2, CountOccurrences(result, "\r\n"));
    }

    [Fact]
    public void Normalize_MixedLineEndings_NormalizesToDetectedCRLF()
    {
        // Auto mode: CRLF appears first, so output uses CRLF
        var options = new FormattingOptions { InsertFinalNewline = false };
        var input = "SELECT *\r\nFROM Users\nWHERE id = 1";
        var result = WhitespaceNormalizer.Normalize(input, options);
        Assert.Contains("\r\n", result); // CRLF detected from first occurrence
        Assert.Equal(2, CountOccurrences(result, "\r\n"));
    }

    [Fact]
    public void Normalize_NoLineEndings_FallbackToCRLF()
    {
        // Auto mode with no line endings falls back to CRLF (Windows-preferred)
        var options = new FormattingOptions { InsertFinalNewline = true };
        var input = "SELECT * FROM Users";
        var result = WhitespaceNormalizer.Normalize(input, options);
        Assert.EndsWith("\r\n", result);
    }

    private static int CountOccurrences(string text, string pattern)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(pattern, index, StringComparison.Ordinal)) != -1)
        {
            count++;
            index += pattern.Length;
        }
        return count;
    }

    #endregion

    #region Protected Regions - String Literals

    [Fact]
    public void Normalize_StringLiteral_PreservesWhitespace()
    {
        var options = new FormattingOptions { TrimTrailingWhitespace = true, InsertFinalNewline = false };
        var input = "SELECT 'text with   spaces   '";
        var result = WhitespaceNormalizer.Normalize(input, options);
        Assert.Contains("'text with   spaces   '", result);
    }

    [Fact]
    public void Normalize_StringLiteralMultiLine_PreservesContent()
    {
        var options = new FormattingOptions { InsertFinalNewline = false };
        var input = "SELECT 'line1\nline2'";
        var result = WhitespaceNormalizer.Normalize(input, options);
        Assert.Contains("'line1", result);
    }

    [Fact]
    public void Normalize_EscapedQuotes_PreservesContent()
    {
        var options = new FormattingOptions { InsertFinalNewline = false };
        var input = "SELECT 'it''s a test   '";
        var result = WhitespaceNormalizer.Normalize(input, options);
        Assert.Contains("'it''s a test   '", result);
    }

    #endregion

    #region Protected Regions - Comments

    [Fact]
    public void Normalize_LineComment_PreservesInternalSpaces()
    {
        var options = new FormattingOptions { TrimTrailingWhitespace = true, InsertFinalNewline = false };
        var input = "SELECT * -- comment with   spaces";
        var result = WhitespaceNormalizer.Normalize(input, options);
        // Line comments preserve internal spaces
        Assert.Contains("-- comment with   spaces", result);
    }

    [Fact]
    public void Normalize_BlockComment_PreservesContent()
    {
        var options = new FormattingOptions { TrimTrailingWhitespace = true, InsertFinalNewline = false };
        var input = "SELECT /* comment with   spaces   */ * FROM Users";
        var result = WhitespaceNormalizer.Normalize(input, options);
        Assert.Contains("/* comment with   spaces   */", result);
    }

    [Fact]
    public void Normalize_MultiLineBlockComment_PreservesContent()
    {
        var options = new FormattingOptions { InsertFinalNewline = false };
        var input = "SELECT /* comment\n   with spaces\n*/ * FROM Users";
        var result = WhitespaceNormalizer.Normalize(input, options);
        Assert.Contains("/* comment", result);
        Assert.Contains("*/", result);
    }

    #endregion

    #region Protected Regions - Identifiers

    [Fact]
    public void Normalize_BracketIdentifier_PreservesContent()
    {
        var options = new FormattingOptions { InsertFinalNewline = false };
        var input = "SELECT [Column With Spaces]";
        var result = WhitespaceNormalizer.Normalize(input, options);
        Assert.Contains("[Column With Spaces]", result);
    }

    [Fact]
    public void Normalize_EscapedBrackets_PreservesContent()
    {
        var options = new FormattingOptions { InsertFinalNewline = false };
        var input = "SELECT [Column]]Name]";
        var result = WhitespaceNormalizer.Normalize(input, options);
        Assert.Contains("[Column]]Name]", result);
    }

    [Fact]
    public void Normalize_DoubleQuoteIdentifier_PreservesContent()
    {
        var options = new FormattingOptions { InsertFinalNewline = false };
        var input = "SELECT \"Column With Spaces\"";
        var result = WhitespaceNormalizer.Normalize(input, options);
        Assert.Contains("\"Column With Spaces\"", result);
    }

    #endregion

    #region Multiline Protected Regions

    [Fact]
    public void Normalize_BlockCommentSpanningLines_PreservesIndent()
    {
        var options = new FormattingOptions { InsertFinalNewline = false };
        var input = "/* comment\n   continues */";
        var result = WhitespaceNormalizer.Normalize(input, options);
        // Inside block comment, whitespace should be preserved
        Assert.Contains("/* comment", result);
        Assert.Contains("   continues */", result);
    }

    [Fact]
    public void Normalize_BlockCommentStartMidLine_PreservesContent()
    {
        var options = new FormattingOptions { InsertFinalNewline = false };
        var input = "SELECT /* start\n   middle\nend */ * FROM Users";
        var result = WhitespaceNormalizer.Normalize(input, options);
        Assert.Contains("/* start", result);
    }

    #endregion

    #region Complex Queries

    [Fact]
    public void Normalize_ComplexQuery_NormalizesCorrectly()
    {
        var options = new FormattingOptions
        {
            IndentStyle = IndentStyle.Spaces,
            IndentSize = 4,
            TrimTrailingWhitespace = true,
            InsertFinalNewline = false
        };

        var input = @"SELECT
    o.OrderId,
    c.CustomerName
FROM dbo.Orders o
WHERE o.Status = 'Active'   ";

        var result = WhitespaceNormalizer.Normalize(input, options);

        Assert.DoesNotContain("   \n", result);
        Assert.Contains("o.OrderId", result);
    }

    [Fact]
    public void Normalize_NestedProtectedRegions_HandlesCorrectly()
    {
        var options = new FormattingOptions { InsertFinalNewline = false };
        var input = "SELECT [Col], 'text', /* comment */ \"Id\"";
        var result = WhitespaceNormalizer.Normalize(input, options);
        Assert.Contains("[Col]", result);
        Assert.Contains("'text'", result);
        Assert.Contains("/* comment */", result);
        Assert.Contains("\"Id\"", result);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Normalize_EmptyLines_PreservesEmptyLines()
    {
        var options = new FormattingOptions { InsertFinalNewline = false };
        var input = "SELECT *\n\nFROM Users";
        var result = WhitespaceNormalizer.Normalize(input, options);
        Assert.Contains("\n\n", result);
    }

    [Fact]
    public void Normalize_OnlyIndentation_TrimsEmptyLine()
    {
        var options = new FormattingOptions { TrimTrailingWhitespace = true, InsertFinalNewline = false };
        var input = "SELECT *\n    \nFROM Users";
        var result = WhitespaceNormalizer.Normalize(input, options);
        Assert.Equal("SELECT *\n\nFROM Users", result);
    }

    [Fact]
    public void Normalize_ZeroIndentSize_DefaultsToFour()
    {
        var options = new FormattingOptions { IndentStyle = IndentStyle.Spaces, IndentSize = 0, InsertFinalNewline = false };
        var input = "\tSELECT"; // 1 tab
        var result = WhitespaceNormalizer.Normalize(input, options);
        Assert.Equal("    SELECT", result); // Defaults to 4 spaces
    }

    [Fact]
    public void Normalize_NegativeIndentSize_DefaultsToFour()
    {
        var options = new FormattingOptions { IndentStyle = IndentStyle.Spaces, IndentSize = -1, InsertFinalNewline = false };
        var input = "\tSELECT"; // 1 tab
        var result = WhitespaceNormalizer.Normalize(input, options);
        Assert.Equal("    SELECT", result); // Defaults to 4 spaces
    }

    #endregion
}
