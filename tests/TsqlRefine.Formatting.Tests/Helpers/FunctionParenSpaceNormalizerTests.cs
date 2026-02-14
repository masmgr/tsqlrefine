using TsqlRefine.Formatting.Helpers.Whitespace;
using Xunit;

namespace TsqlRefine.Formatting.Tests.Helpers;

public class FunctionParenSpaceNormalizerTests
{
    private readonly FormattingOptions _defaultOptions = new();

    // --- Basic edge cases ---

    [Fact]
    public void Normalize_EmptyString_ReturnsEmpty()
    {
        var result = FunctionParenSpaceNormalizer.Normalize("", _defaultOptions);
        Assert.Equal("", result);
    }

    [Fact]
    public void Normalize_NullString_ReturnsNull()
    {
        var result = FunctionParenSpaceNormalizer.Normalize(null!, _defaultOptions);
        Assert.Null(result);
    }

    [Fact]
    public void Normalize_DisabledOption_ReturnsUnchanged()
    {
        var options = new FormattingOptions { NormalizeFunctionSpacing = false };
        var input = "SELECT COUNT (*) FROM users";
        var result = FunctionParenSpaceNormalizer.Normalize(input, options);
        Assert.Equal(input, result);
    }

    // --- Built-in function: basic ---

    [Fact]
    public void Normalize_CountStar_RemovesSpace()
    {
        var input = "SELECT COUNT (*) FROM users";
        var result = FunctionParenSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal("SELECT COUNT(*) FROM users", result);
    }

    [Fact]
    public void Normalize_MultipleFunctions_RemovesSpaces()
    {
        var input = "SELECT COUNT (*), SUM (amount) FROM orders";
        var result = FunctionParenSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal("SELECT COUNT(*), SUM(amount) FROM orders", result);
    }

    [Fact]
    public void Normalize_NoSpace_ReturnsUnchanged()
    {
        var input = "SELECT COUNT(*) FROM users";
        var result = FunctionParenSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal(input, result);
    }

    [Fact]
    public void Normalize_FunctionInWhere_RemovesSpace()
    {
        var input = "SELECT 1 WHERE ISNULL (x, 0) > 0";
        var result = FunctionParenSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal("SELECT 1 WHERE ISNULL(x, 0) > 0", result);
    }

    [Fact]
    public void Normalize_MultipleSpaces_RemovesAll()
    {
        var input = "SELECT COUNT   (*) FROM users";
        var result = FunctionParenSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal("SELECT COUNT(*) FROM users", result);
    }

    [Fact]
    public void Normalize_TabBetweenFunctionAndParen_RemovesTab()
    {
        var input = "SELECT COUNT\t(*) FROM users";
        var result = FunctionParenSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal("SELECT COUNT(*) FROM users", result);
    }

    // --- Protected regions ---

    [Fact]
    public void Normalize_InsideStringLiteral_PreservesContent()
    {
        var input = "SELECT 'COUNT (*)' FROM users";
        var result = FunctionParenSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal("SELECT 'COUNT (*)' FROM users", result);
    }

    [Fact]
    public void Normalize_InsideLineComment_PreservesContent()
    {
        var input = "SELECT 1 -- COUNT (*)";
        var result = FunctionParenSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal("SELECT 1 -- COUNT (*)", result);
    }

    [Fact]
    public void Normalize_InsideBlockComment_PreservesContent()
    {
        var input = "SELECT 1 /* COUNT (*) */ FROM users";
        var result = FunctionParenSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal("SELECT 1 /* COUNT (*) */ FROM users", result);
    }

    // --- CASE should not be affected ---

    [Fact]
    public void Normalize_CaseExpression_PreservesSpace()
    {
        var input = "SELECT CASE (x) WHEN 1 THEN 'a' END FROM t";
        var result = FunctionParenSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal(input, result);
    }

    // --- User-defined functions ---

    [Fact]
    public void Normalize_UserDefinedFunction_RemovesSpace()
    {
        var input = "SELECT dbo.MyFunc (1, 2) FROM t";
        var result = FunctionParenSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal("SELECT dbo.MyFunc(1, 2) FROM t", result);
    }

    [Fact]
    public void Normalize_UserDefinedFunctionNoSchema_RemovesSpace()
    {
        var input = "SELECT MyFunc (1) FROM t";
        var result = FunctionParenSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal("SELECT MyFunc(1) FROM t", result);
    }

    // --- Idempotency ---

    [Fact]
    public void Normalize_AlreadyNormalized_IsIdempotent()
    {
        var input = "SELECT COUNT(*), SUM(amount) FROM orders WHERE ISNULL(x, 0) > 0";
        var first = FunctionParenSpaceNormalizer.Normalize(input, _defaultOptions);
        var second = FunctionParenSpaceNormalizer.Normalize(first, _defaultOptions);
        Assert.Equal(first, second);
    }

    [Fact]
    public void Normalize_WithSpaces_IsIdempotent()
    {
        var input = "SELECT COUNT (*), SUM (amount) FROM orders";
        var first = FunctionParenSpaceNormalizer.Normalize(input, _defaultOptions);
        var second = FunctionParenSpaceNormalizer.Normalize(first, _defaultOptions);
        Assert.Equal(first, second);
    }

    // --- Miscellaneous ---

    [Fact]
    public void Normalize_NestedFunctions_RemovesSpaces()
    {
        var input = "SELECT ISNULL (COUNT (*), 0) FROM t";
        var result = FunctionParenSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal("SELECT ISNULL(COUNT(*), 0) FROM t", result);
    }

    [Fact]
    public void Normalize_WindowFunction_RemovesSpace()
    {
        var input = "SELECT ROW_NUMBER () OVER (ORDER BY id) FROM t";
        var result = FunctionParenSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal("SELECT ROW_NUMBER() OVER (ORDER BY id) FROM t", result);
    }

    [Fact]
    public void Normalize_MultilineInput_RemovesSpaces()
    {
        var input = "SELECT\r\n    COUNT (*)\r\nFROM t";
        var result = FunctionParenSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal("SELECT\r\n    COUNT(*)\r\nFROM t", result);
    }
}
