using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules;
using TsqlRefine.Rules.Rules.Performance;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Performance;

public sealed class AvoidTopInDmlRuleTests
{
    private readonly AvoidTopInDmlRule _rule = new();

    [Fact]
    public void Analyze_UpdateWithTop_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = "UPDATE TOP (10) users SET name = 'John';";
        var context = RuleTestContext.CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
        Assert.Equal("avoid-top-in-dml", diagnostics[0].Code);
        Assert.Contains("UPDATE", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_DeleteWithTop_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = "DELETE TOP (10) FROM users;";
        var context = RuleTestContext.CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
        Assert.Equal("avoid-top-in-dml", diagnostics[0].Code);
        Assert.Contains("DELETE", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_UpdateWithoutTop_ReturnsNoDiagnostic()
    {
        // Arrange
        const string sql = "UPDATE users SET name = 'John' WHERE id = 1;";
        var context = RuleTestContext.CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_DeleteWithoutTop_ReturnsNoDiagnostic()
    {
        // Arrange
        const string sql = "DELETE FROM users WHERE id = 1;";
        var context = RuleTestContext.CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_SelectWithTop_ReturnsNoDiagnostic()
    {
        // Arrange
        const string sql = "SELECT TOP 10 * FROM users;";
        var context = RuleTestContext.CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Metadata_HasCorrectProperties()
    {
        // Assert
        Assert.Equal("avoid-top-in-dml", _rule.Metadata.RuleId);
        Assert.Equal("Performance", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Warning, _rule.Metadata.DefaultSeverity);
        Assert.False(_rule.Metadata.Fixable);
    }

}
