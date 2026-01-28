using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Correctness;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Correctness;

public sealed class DataTypeLengthRuleTests
{
    private readonly DataTypeLengthRule _rule = new();



    [Fact]
    public void Analyze_VarcharWithLength_NoDiagnostics()
    {
        // Arrange
        var sql = "DECLARE @Name VARCHAR(50);";

        // Act
        var diagnostics = _rule.Analyze(RuleTestContext.CreateContext(sql)).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_VarcharWithoutLength_ReturnsDiagnostic()
    {
        // Arrange
        var sql = "DECLARE @Name VARCHAR;";

        // Act
        var diagnostics = _rule.Analyze(RuleTestContext.CreateContext(sql)).ToArray();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("semantic/data-type-length", diagnostic.Code);
        Assert.Contains("VARCHAR", diagnostic.Message);
    }

    [Fact]
    public void Analyze_NvarcharWithoutLength_ReturnsDiagnostic()
    {
        // Arrange
        var sql = "DECLARE @Name NVARCHAR;";

        // Act
        var diagnostics = _rule.Analyze(RuleTestContext.CreateContext(sql)).ToArray();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("semantic/data-type-length", diagnostic.Code);
        Assert.Contains("NVARCHAR", diagnostic.Message);
    }

    [Fact]
    public void Analyze_CharWithoutLength_ReturnsDiagnostic()
    {
        // Arrange
        var sql = "DECLARE @Code CHAR;";

        // Act
        var diagnostics = _rule.Analyze(RuleTestContext.CreateContext(sql)).ToArray();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("semantic/data-type-length", diagnostic.Code);
        Assert.Contains("CHAR", diagnostic.Message);
    }

    [Fact]
    public void Analyze_NcharWithoutLength_ReturnsDiagnostic()
    {
        // Arrange
        var sql = "DECLARE @Code NCHAR;";

        // Act
        var diagnostics = _rule.Analyze(RuleTestContext.CreateContext(sql)).ToArray();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("semantic/data-type-length", diagnostic.Code);
        Assert.Contains("NCHAR", diagnostic.Message);
    }

    [Fact]
    public void Analyze_VarbinaryWithoutLength_ReturnsDiagnostic()
    {
        // Arrange
        var sql = "DECLARE @Data VARBINARY;";

        // Act
        var diagnostics = _rule.Analyze(RuleTestContext.CreateContext(sql)).ToArray();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("semantic/data-type-length", diagnostic.Code);
        Assert.Contains("VARBINARY", diagnostic.Message);
    }

    [Fact]
    public void Analyze_BinaryWithoutLength_ReturnsDiagnostic()
    {
        // Arrange
        var sql = "DECLARE @Data BINARY;";

        // Act
        var diagnostics = _rule.Analyze(RuleTestContext.CreateContext(sql)).ToArray();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("semantic/data-type-length", diagnostic.Code);
        Assert.Contains("BINARY", diagnostic.Message);
    }

    [Fact]
    public void Analyze_VarcharMaxWithLength_NoDiagnostics()
    {
        // Arrange
        var sql = "DECLARE @Name VARCHAR(MAX);";

        // Act
        var diagnostics = _rule.Analyze(RuleTestContext.CreateContext(sql)).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_IntWithoutLength_NoDiagnostics()
    {
        // Arrange
        var sql = "DECLARE @Id INT;";

        // Act
        var diagnostics = _rule.Analyze(RuleTestContext.CreateContext(sql)).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_DateTimeWithoutLength_NoDiagnostics()
    {
        // Arrange
        var sql = "DECLARE @Created DATETIME;";

        // Act
        var diagnostics = _rule.Analyze(RuleTestContext.CreateContext(sql)).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_ColumnDefinitionWithoutLength_ReturnsDiagnostic()
    {
        // Arrange
        var sql = @"
CREATE TABLE Users (
    Name VARCHAR
);";

        // Act
        var diagnostics = _rule.Analyze(RuleTestContext.CreateContext(sql)).ToArray();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("semantic/data-type-length", diagnostic.Code);
    }

    [Fact]
    public void Analyze_MultipleVariablesWithoutLength_ReturnsMultipleDiagnostics()
    {
        // Arrange
        var sql = @"
DECLARE @Name VARCHAR;
DECLARE @Code CHAR;
DECLARE @Data VARBINARY;";

        // Act
        var diagnostics = _rule.Analyze(RuleTestContext.CreateContext(sql)).ToArray();

        // Assert
        Assert.Equal(3, diagnostics.Length);
        Assert.All(diagnostics, d => Assert.Equal("semantic/data-type-length", d.Code));
    }
}
