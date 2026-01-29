using TsqlRefine.Formatting;
using TsqlRefine.Formatting.Helpers;
using Xunit;

namespace TsqlRefine.Formatting.Tests.Helpers;

public class CasingHelpersTests
{
    [Theory]
    [InlineData("SELECT", KeywordCasing.Upper, "SELECT")]
    [InlineData("select", KeywordCasing.Upper, "SELECT")]
    [InlineData("SeLeCt", KeywordCasing.Upper, "SELECT")]
    public void ApplyCasing_KeywordCasing_Upper_ConvertsToUppercase(string input, KeywordCasing casing, string expected)
    {
        var result = CasingHelpers.ApplyCasing(input, casing);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("SELECT", KeywordCasing.Lower, "select")]
    [InlineData("select", KeywordCasing.Lower, "select")]
    [InlineData("SeLeCt", KeywordCasing.Lower, "select")]
    public void ApplyCasing_KeywordCasing_Lower_ConvertsToLowercase(string input, KeywordCasing casing, string expected)
    {
        var result = CasingHelpers.ApplyCasing(input, casing);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("SELECT", KeywordCasing.Pascal, "Select")]
    [InlineData("select", KeywordCasing.Pascal, "Select")]
    [InlineData("SeLeCt", KeywordCasing.Pascal, "Select")]
    public void ApplyCasing_KeywordCasing_Pascal_ConvertsToPascalCase(string input, KeywordCasing casing, string expected)
    {
        var result = CasingHelpers.ApplyCasing(input, casing);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("SELECT", KeywordCasing.Preserve, "SELECT")]
    [InlineData("select", KeywordCasing.Preserve, "select")]
    [InlineData("SeLeCt", KeywordCasing.Preserve, "SeLeCt")]
    public void ApplyCasing_KeywordCasing_Preserve_PreservesOriginal(string input, KeywordCasing casing, string expected)
    {
        var result = CasingHelpers.ApplyCasing(input, casing);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("UserName", IdentifierCasing.Upper, "USERNAME")]
    [InlineData("userName", IdentifierCasing.Upper, "USERNAME")]
    public void ApplyCasing_IdentifierCasing_Upper_ConvertsToUppercase(string input, IdentifierCasing casing, string expected)
    {
        var result = CasingHelpers.ApplyCasing(input, casing);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("UserName", IdentifierCasing.Lower, "username")]
    [InlineData("userName", IdentifierCasing.Lower, "username")]
    public void ApplyCasing_IdentifierCasing_Lower_ConvertsToLowercase(string input, IdentifierCasing casing, string expected)
    {
        var result = CasingHelpers.ApplyCasing(input, casing);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("USERNAME", IdentifierCasing.Pascal, "Username")]
    [InlineData("username", IdentifierCasing.Pascal, "Username")]
    [InlineData("UserName", IdentifierCasing.Pascal, "Username")]
    public void ApplyCasing_IdentifierCasing_Pascal_ConvertsToPascalCase(string input, IdentifierCasing casing, string expected)
    {
        var result = CasingHelpers.ApplyCasing(input, casing);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("USERNAME", IdentifierCasing.Camel, "username")]
    [InlineData("username", IdentifierCasing.Camel, "username")]
    [InlineData("UserName", IdentifierCasing.Camel, "username")]
    public void ApplyCasing_IdentifierCasing_Camel_ConvertsToCamelCase(string input, IdentifierCasing casing, string expected)
    {
        var result = CasingHelpers.ApplyCasing(input, casing);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("UserName", IdentifierCasing.Preserve, "UserName")]
    [InlineData("userName", IdentifierCasing.Preserve, "userName")]
    public void ApplyCasing_IdentifierCasing_Preserve_PreservesOriginal(string input, IdentifierCasing casing, string expected)
    {
        var result = CasingHelpers.ApplyCasing(input, casing);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("a", "A")]
    [InlineData("A", "A")]
    [InlineData("abc", "Abc")]
    [InlineData("ABC", "Abc")]
    public void ToPascalCase_VariousInputs_ReturnsExpected(string input, string expected)
    {
        var result = CasingHelpers.ToPascalCase(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("A", "a")]
    [InlineData("a", "a")]
    [InlineData("ABC", "abc")]
    [InlineData("abc", "abc")]
    public void ToCamelCase_VariousInputs_ReturnsExpected(string input, string expected)
    {
        var result = CasingHelpers.ToCamelCase(input);
        Assert.Equal(expected, result);
    }
}
