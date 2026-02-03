using TsqlRefine.Formatting.Helpers.Registries;
using Xunit;

namespace TsqlRefine.Formatting.Tests.Helpers;

public class DataTypeRegistryTests
{
    #region IsDataType

    [Theory]
    [InlineData("INT")]
    [InlineData("BIGINT")]
    [InlineData("SMALLINT")]
    [InlineData("TINYINT")]
    [InlineData("BIT")]
    public void IsDataType_IntegerType_ReturnsTrue(string typeName)
    {
        Assert.True(DataTypeRegistry.IsDataType(typeName));
    }

    [Theory]
    [InlineData("DECIMAL")]
    [InlineData("NUMERIC")]
    [InlineData("FLOAT")]
    [InlineData("REAL")]
    [InlineData("MONEY")]
    public void IsDataType_NumericType_ReturnsTrue(string typeName)
    {
        Assert.True(DataTypeRegistry.IsDataType(typeName));
    }

    [Theory]
    [InlineData("CHAR")]
    [InlineData("VARCHAR")]
    [InlineData("NCHAR")]
    [InlineData("NVARCHAR")]
    [InlineData("TEXT")]
    public void IsDataType_StringType_ReturnsTrue(string typeName)
    {
        Assert.True(DataTypeRegistry.IsDataType(typeName));
    }

    [Theory]
    [InlineData("DATE")]
    [InlineData("TIME")]
    [InlineData("DATETIME")]
    [InlineData("DATETIME2")]
    [InlineData("DATETIMEOFFSET")]
    public void IsDataType_DateTimeType_ReturnsTrue(string typeName)
    {
        Assert.True(DataTypeRegistry.IsDataType(typeName));
    }

    [Theory]
    [InlineData("int")]
    [InlineData("Int")]
    [InlineData("INT")]
    [InlineData("iNt")]
    public void IsDataType_CaseInsensitive(string typeName)
    {
        Assert.True(DataTypeRegistry.IsDataType(typeName));
    }

    [Theory]
    [InlineData("STRING")]
    [InlineData("BOOLEAN")]
    [InlineData("MyType")]
    [InlineData("")]
    public void IsDataType_NonDataType_ReturnsFalse(string typeName)
    {
        Assert.False(DataTypeRegistry.IsDataType(typeName));
    }

    #endregion

    #region DataTypes Set Coverage

    [Fact]
    public void DataTypes_ContainsExpectedCount()
    {
        // Ensure we have a reasonable number of data types
        Assert.True(DataTypeRegistry.DataTypes.Count >= 30);
    }

    [Theory]
    [InlineData("UNIQUEIDENTIFIER")]
    [InlineData("XML")]
    [InlineData("HIERARCHYID")]
    [InlineData("GEOMETRY")]
    [InlineData("GEOGRAPHY")]
    public void IsDataType_SpecialType_ReturnsTrue(string typeName)
    {
        Assert.True(DataTypeRegistry.IsDataType(typeName));
    }

    #endregion
}
