using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Style;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Style;

public sealed class SemicolonTerminationRuleTests
{
    private readonly SemicolonTerminationRule _rule = new();

    [Fact]
    public void Analyze_SelectWithoutSemicolon_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = "SELECT 1";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
        Assert.Equal("semicolon-termination", diagnostics[0].Code);
        Assert.Contains("semicolon", diagnostics[0].Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Analyze_SelectWithSemicolon_ReturnsNoDiagnostic()
    {
        // Arrange
        const string sql = "SELECT 1;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_MultipleStatementsWithoutSemicolon_ReturnsDiagnostics()
    {
        // Arrange
        const string sql = @"
SELECT 1
SELECT 2
SELECT 3;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Equal(2, diagnostics.Length); // First two statements lack semicolons
    }

    [Fact]
    public void Analyze_MultipleStatementsWithSemicolons_ReturnsNoDiagnostic()
    {
        // Arrange
        const string sql = @"
SELECT 1;
SELECT 2;
SELECT 3;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_UpdateWithoutSemicolon_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = "UPDATE users SET active = 1 WHERE id = 1";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
        Assert.Equal("semicolon-termination", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_InsertWithSemicolon_ReturnsNoDiagnostic()
    {
        // Arrange
        const string sql = "INSERT INTO users (name) VALUES ('test');";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_DeleteWithoutSemicolon_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = "DELETE FROM users WHERE id = 1";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
    }

    [Fact]
    public void Analyze_CreateProcedureBody_StatementsWithoutSemicolon_ReturnsDiagnostics()
    {
        // Arrange
        const string sql = @"
CREATE PROCEDURE GetUser
AS
BEGIN
    SELECT 1
    SELECT 2;
END";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics); // Only the first SELECT lacks semicolon
    }

    [Fact]
    public void Analyze_CTEWithoutSemicolon_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = @"
WITH cte AS (SELECT 1 AS x)
SELECT * FROM cte";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
    }

    [Fact]
    public void Analyze_EmptyInput_ReturnsNoDiagnostic()
    {
        // Arrange
        const string sql = "";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Metadata_HasCorrectProperties()
    {
        // Assert
        Assert.Equal("semicolon-termination", _rule.Metadata.RuleId);
        Assert.Equal("Style", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Information, _rule.Metadata.DefaultSeverity);
        Assert.True(_rule.Metadata.Fixable);
    }

    private static RuleContext CreateContext(string sql)
    {
        return RuleTestContext.CreateContext(sql);
    }
}
