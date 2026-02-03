using TsqlRefine.Formatting;
using TsqlRefine.Formatting.Helpers;
using Xunit;

namespace TsqlRefine.Formatting.Tests.Helpers;

public class OperatorSpaceNormalizerTests
{
    private readonly FormattingOptions _defaultOptions = new();

    [Fact]
    public void Normalize_EmptyString_ReturnsEmpty()
    {
        var result = OperatorSpaceNormalizer.Normalize("", _defaultOptions);
        Assert.Equal("", result);
    }

    [Fact]
    public void Normalize_NullString_ReturnsNull()
    {
        var result = OperatorSpaceNormalizer.Normalize(null!, _defaultOptions);
        Assert.Null(result);
    }

    [Fact]
    public void Normalize_OnlySpaces_HandlesGracefully()
    {
        var result = OperatorSpaceNormalizer.Normalize("   ", _defaultOptions);
        Assert.Equal("   ", result);
    }

    // Basic binary operators - equals
    [Theory]
    [InlineData("WHERE a=b", "WHERE a = b")]
    [InlineData("WHERE a= b", "WHERE a = b")]
    [InlineData("WHERE a =b", "WHERE a = b")]
    [InlineData("WHERE a  =  b", "WHERE a  =  b")]
    public void Normalize_Equals_AddsSpacing(string input, string expected)
    {
        var result = OperatorSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal(expected, result);
    }

    // Basic binary operators - arithmetic
    [Theory]
    [InlineData("SELECT a+b", "SELECT a + b")]
    [InlineData("SELECT a-b", "SELECT a - b")]
    [InlineData("SELECT a*b", "SELECT a * b")]
    [InlineData("SELECT a/b", "SELECT a / b")]
    [InlineData("SELECT a%b", "SELECT a % b")]
    public void Normalize_Arithmetic_AddsSpacing(string input, string expected)
    {
        var result = OperatorSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal(expected, result);
    }

    // Basic binary operators - comparison
    [Theory]
    [InlineData("WHERE a<b", "WHERE a < b")]
    [InlineData("WHERE a>b", "WHERE a > b")]
    public void Normalize_Comparison_AddsSpacing(string input, string expected)
    {
        var result = OperatorSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal(expected, result);
    }

    // Compound operators
    [Theory]
    [InlineData("WHERE a<>b", "WHERE a <> b")]
    [InlineData("WHERE a!=b", "WHERE a != b")]
    [InlineData("WHERE a<=b", "WHERE a <= b")]
    [InlineData("WHERE a>=b", "WHERE a >= b")]
    public void Normalize_CompoundOperators_AddsSpacing(string input, string expected)
    {
        var result = OperatorSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal(expected, result);
    }

    // Unary operators - should NOT add space before
    [Theory]
    [InlineData("SELECT -1", "SELECT -1")]
    [InlineData("SELECT +1", "SELECT +1")]
    [InlineData("WHERE a = -1", "WHERE a = -1")]
    [InlineData("WHERE a > -1", "WHERE a > -1")]
    [InlineData("WHERE a < -1", "WHERE a < -1")]
    [InlineData("SELECT (-1)", "SELECT (-1)")]
    [InlineData("SELECT a, -1", "SELECT a, -1")]  // After comma (already spaced by InlineSpaceNormalizer)
    public void Normalize_UnaryOperators_NoSpaceBefore(string input, string expected)
    {
        var result = OperatorSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal(expected, result);
    }

    // Scientific notation - must preserve
    [Theory]
    [InlineData("SELECT 1e-3", "SELECT 1e-3")]
    [InlineData("SELECT 1E-3", "SELECT 1E-3")]
    [InlineData("SELECT 1e+3", "SELECT 1e+3")]
    [InlineData("SELECT 1E+3", "SELECT 1E+3")]
    [InlineData("SELECT 2.5e-10", "SELECT 2.5e-10")]
    [InlineData("SELECT 2.5E+10", "SELECT 2.5E+10")]
    public void Normalize_ScientificNotation_Preserved(string input, string expected)
    {
        var result = OperatorSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal(expected, result);
    }

    // Scientific notation with following binary operator
    [Theory]
    [InlineData("SELECT 1e-3 + 2", "SELECT 1e-3 + 2")]
    [InlineData("SELECT 1e-3+2", "SELECT 1e-3 + 2")]
    public void Normalize_ScientificNotationWithBinaryOp_Correct(string input, string expected)
    {
        var result = OperatorSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal(expected, result);
    }

    // Protected regions - strings
    [Theory]
    [InlineData("SELECT 'a=b'", "SELECT 'a=b'")]
    [InlineData("SELECT 'a+b'", "SELECT 'a+b'")]
    [InlineData("SELECT 'a<>b'", "SELECT 'a<>b'")]
    public void Normalize_StringLiterals_Preserved(string input, string expected)
    {
        var result = OperatorSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal(expected, result);
    }

    // Protected regions - brackets
    [Theory]
    [InlineData("SELECT [a-b]", "SELECT [a-b]")]
    [InlineData("SELECT [col=name]", "SELECT [col=name]")]
    public void Normalize_BracketedIdentifiers_Preserved(string input, string expected)
    {
        var result = OperatorSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal(expected, result);
    }

    // Protected regions - double quotes
    [Theory]
    [InlineData("SELECT \"a=b\"", "SELECT \"a=b\"")]
    [InlineData("SELECT \"a+b\"", "SELECT \"a+b\"")]
    public void Normalize_DoubleQuotedIdentifiers_Preserved(string input, string expected)
    {
        var result = OperatorSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal(expected, result);
    }

    // Protected regions - block comments
    [Theory]
    [InlineData("SELECT /* a=b */ c", "SELECT /* a=b */ c")]
    [InlineData("SELECT /* a+b */ c", "SELECT /* a+b */ c")]
    public void Normalize_BlockComments_Preserved(string input, string expected)
    {
        var result = OperatorSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal(expected, result);
    }

    // Multi-line block comments - closing */ must not be split
    [Theory]
    [InlineData("comment text */", "comment text */")]
    [InlineData("   */", "   */")]
    [InlineData("*/", "*/")]
    [InlineData("SELECT a */ FROM t", "SELECT a */ FROM t")]
    [InlineData("/*** comment ***/", "/*** comment ***/")]
    public void Normalize_BlockCommentClosing_NotSplit(string input, string expected)
    {
        var result = OperatorSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Normalize_MultilineBlockComment_ClosingPreserved()
    {
        // Simulates second line of a multi-line block comment
        var input = "/* comment\nend of comment */";
        var result = OperatorSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Contains("*/", result);
        Assert.DoesNotContain("* /", result);
    }

    // Protected regions - line comments
    [Theory]
    [InlineData("SELECT a -- a=b", "SELECT a -- a=b")]
    [InlineData("SELECT a --a=b", "SELECT a --a=b")]
    public void Normalize_LineComments_Preserved(string input, string expected)
    {
        var result = OperatorSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal(expected, result);
    }

    // Mixed binary and unary operators
    [Theory]
    [InlineData("SELECT a+-1", "SELECT a + -1")]
    [InlineData("SELECT a*-1", "SELECT a * -1")]
    [InlineData("SELECT a/-1", "SELECT a / -1")]
    [InlineData("SELECT a%-1", "SELECT a % -1")]
    public void Normalize_BinaryThenUnary_CorrectSpacing(string input, string expected)
    {
        var result = OperatorSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal(expected, result);
    }

    // Double minus - binary then unary
    [Theory]
    [InlineData("SELECT a- -1", "SELECT a - -1")]
    public void Normalize_DoubleMinus_CorrectSpacing(string input, string expected)
    {
        var result = OperatorSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal(expected, result);
    }

    // Complex expressions
    [Theory]
    [InlineData("SELECT (a+b)*(c-d)", "SELECT (a + b) * (c - d)")]
    [InlineData("WHERE a=1 AND b=2", "WHERE a = 1 AND b = 2")]
    [InlineData("SET @x=@y+1", "SET @x = @y + 1")]
    [InlineData("UPDATE t SET c=c+1", "UPDATE t SET c = c + 1")]
    public void Normalize_ComplexExpressions_CorrectSpacing(string input, string expected)
    {
        var result = OperatorSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal(expected, result);
    }

    // Edge case: operator at end of line
    [Theory]
    [InlineData("SELECT a+", "SELECT a +")]
    [InlineData("SELECT a=", "SELECT a =")]
    public void Normalize_OperatorAtEndOfLine_NoTrailingSpace(string input, string expected)
    {
        var result = OperatorSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal(expected, result);
    }

    // Multiline queries
    [Fact]
    public void Normalize_MultilineQuery_CorrectSpacing()
    {
        var input = "SELECT a+b\nFROM t\nWHERE x=1";
        var expected = "SELECT a + b\nFROM t\nWHERE x = 1";
        var result = OperatorSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Normalize_WindowsLineEndings_PreservesLineBreaks()
    {
        var input = "SELECT a+b\r\nFROM t";
        var expected = "SELECT a + b\r\nFROM t";
        var result = OperatorSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal(expected, result);
    }

    // Disabled option
    [Fact]
    public void Normalize_DisabledOption_ReturnsUnchanged()
    {
        var input = "SELECT a+b WHERE x=1";
        var options = new FormattingOptions { NormalizeOperatorSpacing = false };
        var result = OperatorSpaceNormalizer.Normalize(input, options);
        Assert.Equal(input, result);
    }

    // Already correctly spaced
    [Theory]
    [InlineData("WHERE a = b", "WHERE a = b")]
    [InlineData("SELECT a + b", "SELECT a + b")]
    [InlineData("WHERE a <> b", "WHERE a <> b")]
    public void Normalize_AlreadyCorrect_Unchanged(string input, string expected)
    {
        var result = OperatorSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal(expected, result);
    }

    // Leading whitespace preservation
    [Theory]
    [InlineData("    SELECT a+b", "    SELECT a + b")]
    [InlineData("\tSELECT a=b", "\tSELECT a = b")]
    public void Normalize_LeadingWhitespace_Preserved(string input, string expected)
    {
        var result = OperatorSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal(expected, result);
    }

    // Unary at line start (after leading whitespace)
    [Theory]
    [InlineData("-1", "-1")]
    [InlineData("    -1", "    -1")]
    public void Normalize_UnaryAtLineStart_NoChange(string input, string expected)
    {
        var result = OperatorSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal(expected, result);
    }

    // Multiple operators in sequence
    [Theory]
    [InlineData("SELECT a+b+c", "SELECT a + b + c")]
    [InlineData("SELECT a*b*c", "SELECT a * b * c")]
    [InlineData("SELECT a+b*c", "SELECT a + b * c")]
    public void Normalize_MultipleOperators_AllSpaced(string input, string expected)
    {
        var result = OperatorSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal(expected, result);
    }

    // Operators after closing parenthesis
    [Theory]
    [InlineData("SELECT (a)+b", "SELECT (a) + b")]
    [InlineData("SELECT (a)+(b)", "SELECT (a) + (b)")]
    public void Normalize_AfterClosingParen_Binary(string input, string expected)
    {
        var result = OperatorSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal(expected, result);
    }

    // Operators after closing bracket
    [Theory]
    [InlineData("SELECT [a]+b", "SELECT [a] + b")]
    [InlineData("SELECT [a]+[b]", "SELECT [a] + [b]")]
    public void Normalize_AfterClosingBracket_Binary(string input, string expected)
    {
        var result = OperatorSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal(expected, result);
    }

    // Edge case with escaped quotes
    [Theory]
    [InlineData("SELECT 'it''s'+b", "SELECT 'it''s' + b")]
    public void Normalize_EscapedQuotes_HandlesProperly(string input, string expected)
    {
        var result = OperatorSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal(expected, result);
    }

    // CASE WHEN with unary
    [Theory]
    [InlineData("CASE WHEN -1 THEN 0 END", "CASE WHEN -1 THEN 0 END")]
    [InlineData("CASE WHEN a=-1 THEN 0 END", "CASE WHEN a = -1 THEN 0 END")]
    public void Normalize_CaseWhen_CorrectSpacing(string input, string expected)
    {
        var result = OperatorSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal(expected, result);
    }

    // Asterisk after open paren - COUNT(*), SUM(*), etc.
    [Theory]
    [InlineData("SELECT COUNT(*)", "SELECT COUNT(*)")]
    [InlineData("SELECT SUM(*)", "SELECT SUM(*)")]
    [InlineData("SELECT COUNT( * )", "SELECT COUNT( * )")]  // Already has spaces, preserve
    public void Normalize_AsteriskAfterOpenParen_NoSpacing(string input, string expected)
    {
        var result = OperatorSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal(expected, result);
    }

    // Asterisk as multiplication (not after open paren)
    [Theory]
    [InlineData("SELECT a*b", "SELECT a * b")]
    [InlineData("SELECT (a)*b", "SELECT (a) * b")]
    public void Normalize_AsteriskMultiplication_HasSpacing(string input, string expected)
    {
        var result = OperatorSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal(expected, result);
    }

    // Multi-line block comment - asterisks should not be split
    [Fact]
    public void Normalize_MultilineBlockComment_AsterisksPreserved()
    {
        var input = "/***\n* COMMENT\n***/";
        var result = OperatorSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal("/***\n* COMMENT\n***/", result);
    }

    [Fact]
    public void Normalize_MultilineBlockComment_WindowsLineEndings_AsterisksPreserved()
    {
        var input = "/***\r\n* COMMENT\r\n***/";
        var result = OperatorSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal("/***\r\n* COMMENT\r\n***/", result);
    }

    // Alignment preservation
    [Theory]
    [InlineData("SET @a          = 1", "SET @a          = 1")]
    [InlineData("WHERE col       = 'value'", "WHERE col       = 'value'")]
    [InlineData("SELECT a        + b", "SELECT a        + b")]
    public void Normalize_AlignedOperators_PreservesAlignment(string input, string expected)
    {
        var result = OperatorSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal(expected, result);
    }
}
