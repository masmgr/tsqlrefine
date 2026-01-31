using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.Rules.Helpers;
using Xunit;

namespace TsqlRefine.Rules.Tests.Helpers;

public sealed class ExpressionAnalysisHelpersTests
{
    private static ScalarExpression? GetWhereExpression(string sql)
    {
        var parser = new TSql160Parser(true);
        var fragment = parser.Parse(new System.IO.StringReader(sql), out var errors);
        var script = (TSqlScript)fragment;
        var batch = script.Batches[0];
        var selectStatement = (SelectStatement)batch.Statements[0];
        var querySpec = (QuerySpecification)selectStatement.QueryExpression;
        var whereClause = querySpec.WhereClause;
        if (whereClause?.SearchCondition is BooleanComparisonExpression comparison)
        {
            return comparison.FirstExpression;
        }
        return null;
    }

    [Fact]
    public void ContainsColumnReference_WithNull_ReturnsFalse()
    {
        // Act
        var result = ExpressionAnalysisHelpers.ContainsColumnReference(null);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ContainsColumnReference_WithDirectColumnReference_ReturnsTrue()
    {
        // Arrange
        var expression = GetWhereExpression("SELECT * FROM users WHERE name = 'test'");

        // Act
        var result = ExpressionAnalysisHelpers.ContainsColumnReference(expression);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ContainsColumnReference_WithLiteral_ReturnsFalse()
    {
        // Arrange
        var expression = GetWhereExpression("SELECT * FROM users WHERE 1 = 1");

        // Act
        var result = ExpressionAnalysisHelpers.ContainsColumnReference(expression);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ContainsColumnReference_WithCastOnColumn_ReturnsTrue()
    {
        // Arrange
        var expression = GetWhereExpression("SELECT * FROM users WHERE CAST(id AS VARCHAR) = '1'");

        // Act
        var result = ExpressionAnalysisHelpers.ContainsColumnReference(expression);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ContainsColumnReference_WithCastOnLiteral_ReturnsFalse()
    {
        // Arrange
        var expression = GetWhereExpression("SELECT * FROM users WHERE CAST(1 AS VARCHAR) = id");

        // Act
        var result = ExpressionAnalysisHelpers.ContainsColumnReference(expression);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ContainsColumnReference_WithConvertOnColumn_ReturnsTrue()
    {
        // Arrange
        var expression = GetWhereExpression("SELECT * FROM users WHERE CONVERT(VARCHAR, id) = '1'");

        // Act
        var result = ExpressionAnalysisHelpers.ContainsColumnReference(expression);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ContainsColumnReference_WithFunctionOnColumn_ReturnsTrue()
    {
        // Arrange
        var expression = GetWhereExpression("SELECT * FROM users WHERE UPPER(name) = 'TEST'");

        // Act
        var result = ExpressionAnalysisHelpers.ContainsColumnReference(expression);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ContainsColumnReference_WithFunctionOnLiteral_ReturnsFalse()
    {
        // Arrange
        var expression = GetWhereExpression("SELECT * FROM users WHERE UPPER('test') = name");

        // Act
        var result = ExpressionAnalysisHelpers.ContainsColumnReference(expression);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ContainsColumnReference_WithBinaryExpressionContainingColumn_ReturnsTrue()
    {
        // Arrange
        var expression = GetWhereExpression("SELECT * FROM users WHERE (id + 1) = 5");

        // Act
        var result = ExpressionAnalysisHelpers.ContainsColumnReference(expression);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ContainsColumnReference_WithParenthesisExpressionContainingColumn_ReturnsTrue()
    {
        // Arrange
        var expression = GetWhereExpression("SELECT * FROM users WHERE (id) = 5");

        // Act
        var result = ExpressionAnalysisHelpers.ContainsColumnReference(expression);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ContainsColumnReference_WithNestedFunctions_ReturnsTrue()
    {
        // Arrange
        var expression = GetWhereExpression("SELECT * FROM users WHERE UPPER(LTRIM(name)) = 'TEST'");

        // Act
        var result = ExpressionAnalysisHelpers.ContainsColumnReference(expression);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ContainsColumnReference_WithDatePartFunction_IgnoresDatePartLiteral()
    {
        // Arrange - DATEPART(year, date_column) - the 'year' is a datepart literal, not a column
        var expression = GetWhereExpression("SELECT * FROM orders WHERE DATEPART(year, order_date) = 2023");

        // Act
        var result = ExpressionAnalysisHelpers.ContainsColumnReference(expression);

        // Assert
        Assert.True(result); // Contains order_date column reference
    }
}
