using TsqlRefine.Formatting.Helpers;
using Xunit;

namespace TsqlRefine.Formatting.Tests.Helpers;

public class SystemSchemaRegistryTests
{
    #region IsSystemSchema

    [Theory]
    [InlineData("sys")]
    [InlineData("information_schema")]
    public void IsSystemSchema_SystemSchema_ReturnsTrue(string schemaName)
    {
        Assert.True(SystemSchemaRegistry.IsSystemSchema(schemaName));
    }

    [Theory]
    [InlineData("SYS")]
    [InlineData("Sys")]
    [InlineData("INFORMATION_SCHEMA")]
    [InlineData("Information_Schema")]
    public void IsSystemSchema_CaseInsensitive(string schemaName)
    {
        Assert.True(SystemSchemaRegistry.IsSystemSchema(schemaName));
    }

    [Theory]
    [InlineData("dbo")]
    [InlineData("public")]
    [InlineData("MySchema")]
    [InlineData("staging")]
    public void IsSystemSchema_NonSystemSchema_ReturnsFalse(string schemaName)
    {
        Assert.False(SystemSchemaRegistry.IsSystemSchema(schemaName));
    }

    [Fact]
    public void IsSystemSchema_NullSchema_ReturnsFalse()
    {
        Assert.False(SystemSchemaRegistry.IsSystemSchema(null));
    }

    [Fact]
    public void IsSystemSchema_EmptySchema_ReturnsFalse()
    {
        Assert.False(SystemSchemaRegistry.IsSystemSchema(""));
    }

    #endregion

    #region SystemSchemas Set Coverage

    [Fact]
    public void SystemSchemas_ContainsExpectedSchemas()
    {
        Assert.Equal(2, SystemSchemaRegistry.SystemSchemas.Count);
        Assert.Contains("sys", (IEnumerable<string>)SystemSchemaRegistry.SystemSchemas);
        Assert.Contains("information_schema", (IEnumerable<string>)SystemSchemaRegistry.SystemSchemas);
    }

    #endregion
}
