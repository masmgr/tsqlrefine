using TsqlRefine.Formatting.Helpers.Casing;
using Xunit;

namespace TsqlRefine.Formatting.Tests.Helpers;

public class CasingHelpersTests
{
    #region ElementCasing.Upper

    [Fact]
    public void ApplyCasing_Upper_ConvertsToUppercase()
    {
        var result = CasingHelpers.ApplyCasing("select", ElementCasing.Upper);
        Assert.Equal("SELECT", result);
    }

    [Fact]
    public void ApplyCasing_Upper_MixedCase()
    {
        var result = CasingHelpers.ApplyCasing("SeLeCt", ElementCasing.Upper);
        Assert.Equal("SELECT", result);
    }

    [Fact]
    public void ApplyCasing_Upper_AlreadyUppercase()
    {
        var result = CasingHelpers.ApplyCasing("SELECT", ElementCasing.Upper);
        Assert.Equal("SELECT", result);
    }

    [Theory]
    [InlineData("from", "FROM")]
    [InlineData("where", "WHERE")]
    [InlineData("inner join", "INNER JOIN")]
    [InlineData("varchar", "VARCHAR")]
    public void ApplyCasing_Upper_Keywords(string input, string expected)
    {
        var result = CasingHelpers.ApplyCasing(input, ElementCasing.Upper);
        Assert.Equal(expected, result);
    }

    #endregion

    #region ElementCasing.Lower

    [Fact]
    public void ApplyCasing_Lower_ConvertsToLowercase()
    {
        var result = CasingHelpers.ApplyCasing("SELECT", ElementCasing.Lower);
        Assert.Equal("select", result);
    }

    [Fact]
    public void ApplyCasing_Lower_MixedCase()
    {
        var result = CasingHelpers.ApplyCasing("SeLeCt", ElementCasing.Lower);
        Assert.Equal("select", result);
    }

    [Fact]
    public void ApplyCasing_Lower_AlreadyLowercase()
    {
        var result = CasingHelpers.ApplyCasing("select", ElementCasing.Lower);
        Assert.Equal("select", result);
    }

    [Theory]
    [InlineData("FROM", "from")]
    [InlineData("WHERE", "where")]
    [InlineData("INNER JOIN", "inner join")]
    [InlineData("VARCHAR", "varchar")]
    public void ApplyCasing_Lower_Keywords(string input, string expected)
    {
        var result = CasingHelpers.ApplyCasing(input, ElementCasing.Lower);
        Assert.Equal(expected, result);
    }

    #endregion

    #region ElementCasing.None

    [Fact]
    public void ApplyCasing_None_PreservesOriginal()
    {
        var result = CasingHelpers.ApplyCasing("SeLeCt", ElementCasing.None);
        Assert.Equal("SeLeCt", result);
    }

    [Fact]
    public void ApplyCasing_None_UppercasePreserved()
    {
        var result = CasingHelpers.ApplyCasing("SELECT", ElementCasing.None);
        Assert.Equal("SELECT", result);
    }

    [Fact]
    public void ApplyCasing_None_LowercasePreserved()
    {
        var result = CasingHelpers.ApplyCasing("select", ElementCasing.None);
        Assert.Equal("select", result);
    }

    [Theory]
    [InlineData("SeLeCt")]
    [InlineData("select")]
    [InlineData("SELECT")]
    [InlineData("CustomerName")]
    [InlineData("customer_name")]
    public void ApplyCasing_None_VariousInputs_Preserved(string input)
    {
        var result = CasingHelpers.ApplyCasing(input, ElementCasing.None);
        Assert.Equal(input, result);
    }

    #endregion

    #region Empty and Special Characters

    [Fact]
    public void ApplyCasing_EmptyString_ReturnsEmpty()
    {
        Assert.Equal("", CasingHelpers.ApplyCasing("", ElementCasing.Upper));
        Assert.Equal("", CasingHelpers.ApplyCasing("", ElementCasing.Lower));
        Assert.Equal("", CasingHelpers.ApplyCasing("", ElementCasing.None));
    }

    [Fact]
    public void ApplyCasing_WhitespaceOnly_PreservesWhitespace()
    {
        Assert.Equal("   ", CasingHelpers.ApplyCasing("   ", ElementCasing.Upper));
        Assert.Equal("   ", CasingHelpers.ApplyCasing("   ", ElementCasing.Lower));
        Assert.Equal("   ", CasingHelpers.ApplyCasing("   ", ElementCasing.None));
    }

    [Fact]
    public void ApplyCasing_Numbers_PreservesNumbers()
    {
        Assert.Equal("123", CasingHelpers.ApplyCasing("123", ElementCasing.Upper));
        Assert.Equal("123", CasingHelpers.ApplyCasing("123", ElementCasing.Lower));
        Assert.Equal("123", CasingHelpers.ApplyCasing("123", ElementCasing.None));
    }

    [Fact]
    public void ApplyCasing_SpecialCharacters_PreservesSpecialCharacters()
    {
        var input = "!@#$%^&*()";
        Assert.Equal(input, CasingHelpers.ApplyCasing(input, ElementCasing.Upper));
        Assert.Equal(input, CasingHelpers.ApplyCasing(input, ElementCasing.Lower));
        Assert.Equal(input, CasingHelpers.ApplyCasing(input, ElementCasing.None));
    }

    [Fact]
    public void ApplyCasing_MixedAlphanumeric_HandlesCorrectly()
    {
        Assert.Equal("TABLE123", CasingHelpers.ApplyCasing("table123", ElementCasing.Upper));
        Assert.Equal("table123", CasingHelpers.ApplyCasing("TABLE123", ElementCasing.Lower));
        Assert.Equal("Table123", CasingHelpers.ApplyCasing("Table123", ElementCasing.None));
    }

    #endregion

    #region Unicode and Non-ASCII Characters

    [Fact]
    public void ApplyCasing_Upper_NonAscii()
    {
        var result = CasingHelpers.ApplyCasing("caf\u00e9", ElementCasing.Upper);
        Assert.Equal("CAF\u00c9", result);
    }

    [Fact]
    public void ApplyCasing_Lower_NonAscii()
    {
        var result = CasingHelpers.ApplyCasing("CAF\u00c9", ElementCasing.Lower);
        Assert.Equal("caf\u00e9", result);
    }

    [Fact]
    public void ApplyCasing_German_Eszett()
    {
        // German sharp s (ß) uppercases - behavior varies by .NET version
        var result = CasingHelpers.ApplyCasing("stra\u00dfe", ElementCasing.Upper);
        // .NET 10: STRAßE (ß preserved), older .NET: STRASSE, some versions: STRAẞE
        Assert.True(
            result == "STRASSE" || result == "STRA\u1E9EE" || result == "STRA\u00dfE",
            $"Unexpected result: {result}");
    }

    #endregion

    #region Default/Invalid Casing Value

    [Fact]
    public void ApplyCasing_InvalidEnumValue_ReturnsOriginal()
    {
        // Cast an invalid enum value
        var invalidCasing = (ElementCasing)999;
        var result = CasingHelpers.ApplyCasing("Test", invalidCasing);
        Assert.Equal("Test", result);
    }

    #endregion
}
