using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.Formatting.Helpers;
using Xunit;

namespace TsqlRefine.Formatting.Tests.Helpers;

public class AstPositionMapTests
{
    private static TSqlFragment? ParseSql(string sql)
    {
        var parser = new TSql160Parser(initialQuotedIdentifiers: false);
        using var reader = new StringReader(sql);
        var fragment = parser.Parse(reader, out var errors);
        return errors.Count > 0 ? null : fragment;
    }

    [Fact]
    public void Build_NullFragment_ReturnsNull()
    {
        var result = AstPositionMap.Build(null);
        Assert.Null(result);
    }

    [Fact]
    public void Build_ValidFragment_ReturnsMap()
    {
        var fragment = ParseSql("SELECT 1");
        var result = AstPositionMap.Build(fragment);
        Assert.NotNull(result);
    }

    [Fact]
    public void GetContext_UnknownPosition_ReturnsUnknown()
    {
        var fragment = ParseSql("SELECT 1");
        var map = AstPositionMap.Build(fragment);

        Assert.NotNull(map);
        var context = map.GetContext(999, 999);
        Assert.Equal(AstPositionMap.OperatorContext.Unknown, context);
    }

    // Unary operator detection
    [Theory]
    [InlineData("SELECT -1")]
    [InlineData("SELECT +1")]
    public void Build_UnaryExpression_DetectsUnarySign(string sql)
    {
        var fragment = ParseSql(sql);
        var map = AstPositionMap.Build(fragment);

        Assert.NotNull(map);
        // Unary operator is at the start of the expression
        // In "SELECT -1", the - is at column 8 (1-based)
        var context = map.GetContext(1, 8);
        Assert.Equal(AstPositionMap.OperatorContext.UnarySign, context);
    }

    // Binary arithmetic operator detection
    [Theory]
    [InlineData("SELECT a + b", 10)]
    [InlineData("SELECT a - b", 10)]
    [InlineData("SELECT a * b", 10)]
    [InlineData("SELECT a / b", 10)]
    public void Build_BinaryExpression_DetectsBinaryArithmetic(string sql, int expectedColumn)
    {
        var fragment = ParseSql(sql);
        var map = AstPositionMap.Build(fragment);

        Assert.NotNull(map);
        var context = map.GetContext(1, expectedColumn);
        Assert.Equal(AstPositionMap.OperatorContext.BinaryArithmetic, context);
    }

    // Comparison operator detection
    [Theory]
    [InlineData("SELECT 1 WHERE a = b")]
    [InlineData("SELECT 1 WHERE a > b")]
    [InlineData("SELECT 1 WHERE a < b")]
    public void Build_ComparisonExpression_DetectsComparison(string sql)
    {
        var fragment = ParseSql(sql);
        var map = AstPositionMap.Build(fragment);

        Assert.NotNull(map);
        // Check that at least one comparison context is detected
        // The exact position depends on the SQL structure
        var hasComparison = false;
        for (var col = 1; col <= sql.Length; col++)
        {
            if (map.GetContext(1, col) == AstPositionMap.OperatorContext.Comparison)
            {
                hasComparison = true;
                break;
            }
        }
        Assert.True(hasComparison, "Expected at least one Comparison context");
    }

    // SELECT * detection
    [Fact]
    public void Build_SelectStar_DetectsSelectStar()
    {
        var fragment = ParseSql("SELECT * FROM t");
        var map = AstPositionMap.Build(fragment);

        Assert.NotNull(map);
        // In "SELECT * FROM t", the * is at column 8 (1-based)
        var context = map.GetContext(1, 8);
        Assert.Equal(AstPositionMap.OperatorContext.SelectStar, context);
    }

    // SELECT t.* detection
    [Fact]
    public void Build_QualifiedStar_DetectsQualifiedStar()
    {
        var fragment = ParseSql("SELECT t.* FROM t");
        var map = AstPositionMap.Build(fragment);

        Assert.NotNull(map);
        // In "SELECT t.* FROM t", the t.* starts at column 8
        // The * itself is at column 10 (after "t.")
        var hasQualifiedStar = false;
        for (var col = 8; col <= 12; col++)
        {
            if (map.GetContext(1, col) == AstPositionMap.OperatorContext.QualifiedStar)
            {
                hasQualifiedStar = true;
                break;
            }
        }
        Assert.True(hasQualifiedStar, "Expected QualifiedStar context");
    }

    // COUNT(*) detection
    [Fact]
    public void Build_FunctionStar_DetectsFunctionStar()
    {
        var fragment = ParseSql("SELECT COUNT(*) FROM t");
        var map = AstPositionMap.Build(fragment);

        Assert.NotNull(map);
        // Look for FunctionStar context around the COUNT(*) position
        var hasFunctionStar = false;
        for (var col = 8; col <= 16; col++)
        {
            if (map.GetContext(1, col) == AstPositionMap.OperatorContext.FunctionStar)
            {
                hasFunctionStar = true;
                break;
            }
        }
        Assert.True(hasFunctionStar, "Expected FunctionStar context for COUNT(*)");
    }

    // Mixed expressions
    [Fact]
    public void Build_MixedBinaryAndUnary_CorrectlyDistinguishes()
    {
        // "SELECT a + -1" has binary + and unary -
        var fragment = ParseSql("SELECT a + -1");
        var map = AstPositionMap.Build(fragment);

        Assert.NotNull(map);

        // Find the contexts
        var hasBinary = false;
        var hasUnary = false;
        for (var col = 1; col <= 13; col++)
        {
            var ctx = map.GetContext(1, col);
            if (ctx == AstPositionMap.OperatorContext.BinaryArithmetic)
                hasBinary = true;
            if (ctx == AstPositionMap.OperatorContext.UnarySign)
                hasUnary = true;
        }

        Assert.True(hasBinary, "Expected BinaryArithmetic context for +");
        Assert.True(hasUnary, "Expected UnarySign context for -");
    }

    // Complex expression
    [Fact]
    public void Build_ComplexExpression_DetectsMultipleOperators()
    {
        var fragment = ParseSql("SELECT (a + b) * (c - d)");
        var map = AstPositionMap.Build(fragment);

        Assert.NotNull(map);

        // Count binary operators detected
        var binaryCount = 0;
        for (var col = 1; col <= 25; col++)
        {
            if (map.GetContext(1, col) == AstPositionMap.OperatorContext.BinaryArithmetic)
                binaryCount++;
        }

        // Should detect at least 3 binary operators: +, *, -
        Assert.True(binaryCount >= 3, $"Expected at least 3 binary operators, found {binaryCount}");
    }

    // Integration test: Format with AST
    [Theory]
    [InlineData("SELECT a+b", "SELECT a + b")]
    [InlineData("SELECT -1", "SELECT -1")]
    [InlineData("SELECT COUNT(*)", "SELECT COUNT(*)")]
    public void Format_WithAst_ProducesCorrectOutput(string input, string expected)
    {
        var fragment = ParseSql(input);
        var result = SqlFormatter.Format(input, new FormattingOptions(), fragment);

        // Note: The formatter also applies casing and other transformations
        // So we check case-insensitively and focusing on spacing
        Assert.Equal(expected.ToUpperInvariant(), result.ToUpperInvariant());
    }
}
