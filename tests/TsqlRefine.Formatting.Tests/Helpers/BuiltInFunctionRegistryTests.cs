using TsqlRefine.Formatting.Helpers.Registries;
using Xunit;

namespace TsqlRefine.Formatting.Tests.Helpers;

public class BuiltInFunctionRegistryTests
{
    #region IsBuiltInFunction

    [Theory]
    [InlineData("COUNT")]
    [InlineData("SUM")]
    [InlineData("AVG")]
    [InlineData("MIN")]
    [InlineData("MAX")]
    public void IsBuiltInFunction_AggregateFunction_ReturnsTrue(string functionName)
    {
        Assert.True(BuiltInFunctionRegistry.IsBuiltInFunction(functionName));
    }

    [Theory]
    [InlineData("LEN")]
    [InlineData("SUBSTRING")]
    [InlineData("CONCAT")]
    [InlineData("UPPER")]
    [InlineData("LOWER")]
    public void IsBuiltInFunction_StringFunction_ReturnsTrue(string functionName)
    {
        Assert.True(BuiltInFunctionRegistry.IsBuiltInFunction(functionName));
    }

    [Theory]
    [InlineData("GETDATE")]
    [InlineData("DATEADD")]
    [InlineData("DATEDIFF")]
    [InlineData("YEAR")]
    [InlineData("MONTH")]
    public void IsBuiltInFunction_DateFunction_ReturnsTrue(string functionName)
    {
        Assert.True(BuiltInFunctionRegistry.IsBuiltInFunction(functionName));
    }

    [Theory]
    [InlineData("CAST")]
    [InlineData("CONVERT")]
    [InlineData("ISNULL")]
    [InlineData("COALESCE")]
    public void IsBuiltInFunction_ConversionAndNullFunction_ReturnsTrue(string functionName)
    {
        Assert.True(BuiltInFunctionRegistry.IsBuiltInFunction(functionName));
    }

    [Theory]
    [InlineData("count")]
    [InlineData("Count")]
    [InlineData("COUNT")]
    [InlineData("CoUnT")]
    public void IsBuiltInFunction_CaseInsensitive(string functionName)
    {
        Assert.True(BuiltInFunctionRegistry.IsBuiltInFunction(functionName));
    }

    [Theory]
    [InlineData("MyFunction")]
    [InlineData("udf_GetData")]
    [InlineData("sp_help")]
    [InlineData("")]
    public void IsBuiltInFunction_NonBuiltInFunction_ReturnsFalse(string functionName)
    {
        Assert.False(BuiltInFunctionRegistry.IsBuiltInFunction(functionName));
    }

    #endregion

    #region IsParenthesisFreeFunction

    [Theory]
    [InlineData("CURRENT_TIMESTAMP")]
    [InlineData("CURRENT_USER")]
    [InlineData("SESSION_USER")]
    [InlineData("SYSTEM_USER")]
    [InlineData("USER")]
    public void IsParenthesisFreeFunction_NiladicFunction_ReturnsTrue(string functionName)
    {
        Assert.True(BuiltInFunctionRegistry.IsParenthesisFreeFunction(functionName));
    }

    [Theory]
    [InlineData("current_timestamp")]
    [InlineData("Current_Timestamp")]
    [InlineData("CURRENT_TIMESTAMP")]
    public void IsParenthesisFreeFunction_CaseInsensitive(string functionName)
    {
        Assert.True(BuiltInFunctionRegistry.IsParenthesisFreeFunction(functionName));
    }

    [Theory]
    [InlineData("COUNT")]
    [InlineData("GETDATE")]
    [InlineData("MyFunction")]
    public void IsParenthesisFreeFunction_RegularFunction_ReturnsFalse(string functionName)
    {
        Assert.False(BuiltInFunctionRegistry.IsParenthesisFreeFunction(functionName));
    }

    #endregion

    #region Functions Set Coverage

    [Fact]
    public void Functions_ContainsExpectedCount()
    {
        // Ensure we have a reasonable number of built-in functions
        Assert.True(BuiltInFunctionRegistry.Functions.Count >= 70);
    }

    [Fact]
    public void ParenthesisFreeFunctions_ContainsExpectedCount()
    {
        Assert.Equal(5, BuiltInFunctionRegistry.ParenthesisFreeFunctions.Count);
    }

    #endregion
}
