using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace TsqlRefine.Rules.Tests.Helpers;

public sealed class DatePartHelperTests
{
    #region IsDatePartFunction Tests

    [Theory]
    [InlineData("DATEADD")]
    [InlineData("DATEDIFF")]
    [InlineData("DATEPART")]
    [InlineData("DATENAME")]
    public void IsDatePartFunction_WithDatePartFunction_ReturnsTrue(string functionName)
    {
        // Arrange
        var func = CreateFunctionCall(functionName);

        // Act
        var result = DatePartHelper.IsDatePartFunction(func);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData("dateadd")]
    [InlineData("DateAdd")]
    [InlineData("DATEADD")]
    [InlineData("dAtEaDd")]
    public void IsDatePartFunction_IsCaseInsensitive_ReturnsTrue(string functionName)
    {
        // Arrange
        var func = CreateFunctionCall(functionName);

        // Act
        var result = DatePartHelper.IsDatePartFunction(func);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData("GETDATE")]
    [InlineData("CONVERT")]
    [InlineData("CAST")]
    [InlineData("ISNULL")]
    [InlineData("YEAR")]
    [InlineData("MONTH")]
    [InlineData("DAY")]
    public void IsDatePartFunction_WithNonDatePartFunction_ReturnsFalse(string functionName)
    {
        // Arrange
        var func = CreateFunctionCall(functionName);

        // Act
        var result = DatePartHelper.IsDatePartFunction(func);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsDatePartFunction_WithNullFunctionName_ReturnsFalse()
    {
        // Arrange
        var func = new FunctionCall { FunctionName = null };

        // Act
        var result = DatePartHelper.IsDatePartFunction(func);

        // Assert
        Assert.False(result);
    }

    #endregion

    #region IsDatePartLiteralParameter Tests

    [Fact]
    public void IsDatePartLiteralParameter_WithFirstParamColumnRef_ReturnsTrue()
    {
        // Arrange
        var func = CreateFunctionCall("DATEADD");
        var param = CreateSinglePartColumnReference("year");

        // Act
        var result = DatePartHelper.IsDatePartLiteralParameter(func, 0, param);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsDatePartLiteralParameter_WithSecondParamColumnRef_ReturnsFalse()
    {
        // Arrange
        var func = CreateFunctionCall("DATEADD");
        var param = CreateSinglePartColumnReference("year");

        // Act
        var result = DatePartHelper.IsDatePartLiteralParameter(func, 1, param);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsDatePartLiteralParameter_WithNonDatePartFunction_ReturnsFalse()
    {
        // Arrange
        var func = CreateFunctionCall("GETDATE");
        var param = CreateSinglePartColumnReference("year");

        // Act
        var result = DatePartHelper.IsDatePartLiteralParameter(func, 0, param);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsDatePartLiteralParameter_WithMultiPartColumnRef_ReturnsFalse()
    {
        // Arrange
        var func = CreateFunctionCall("DATEADD");
        var param = CreateMultiPartColumnReference("t", "year");

        // Act
        var result = DatePartHelper.IsDatePartLiteralParameter(func, 0, param);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsDatePartLiteralParameter_WithLiteralExpression_ReturnsFalse()
    {
        // Arrange
        var func = CreateFunctionCall("DATEADD");
        var param = new IntegerLiteral { Value = "1" };

        // Act
        var result = DatePartHelper.IsDatePartLiteralParameter(func, 0, param);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData("DATEADD")]
    [InlineData("DATEDIFF")]
    [InlineData("DATEPART")]
    [InlineData("DATENAME")]
    public void IsDatePartLiteralParameter_WorksForAllDatePartFunctions(string functionName)
    {
        // Arrange
        var func = CreateFunctionCall(functionName);
        var param = CreateSinglePartColumnReference("month");

        // Act
        var result = DatePartHelper.IsDatePartLiteralParameter(func, 0, param);

        // Assert
        Assert.True(result);
    }

    #endregion

    #region Helper Methods

    private static FunctionCall CreateFunctionCall(string name)
    {
        return new FunctionCall
        {
            FunctionName = new Identifier { Value = name }
        };
    }

    private static ColumnReferenceExpression CreateSinglePartColumnReference(string name)
    {
        return new ColumnReferenceExpression
        {
            MultiPartIdentifier = new MultiPartIdentifier
            {
                Identifiers = { new Identifier { Value = name } }
            }
        };
    }

    private static ColumnReferenceExpression CreateMultiPartColumnReference(params string[] names)
    {
        var colRef = new ColumnReferenceExpression
        {
            MultiPartIdentifier = new MultiPartIdentifier()
        };

        foreach (var name in names)
        {
            colRef.MultiPartIdentifier.Identifiers.Add(new Identifier { Value = name });
        }

        return colRef;
    }

    #endregion
}
