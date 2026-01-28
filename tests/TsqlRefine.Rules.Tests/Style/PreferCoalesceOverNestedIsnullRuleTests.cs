using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Style;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Style;

public sealed class PreferCoalesceOverNestedIsnullRuleTests
{
    private readonly PreferCoalesceOverNestedIsnullRule _rule = new();

    [Fact]
    public void Analyze_NestedIsnull_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = "SELECT ISNULL(ISNULL(@value1, @value2), @value3) FROM users;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
        Assert.Equal("prefer-coalesce-over-nested-isnull", diagnostics[0].Code);
        Assert.Contains("COALESCE", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_SingleIsnull_ReturnsNoDiagnostic()
    {
        // Arrange
        const string sql = "SELECT ISNULL(@value, 'default') FROM users;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_Coalesce_ReturnsNoDiagnostic()
    {
        // Arrange
        const string sql = "SELECT COALESCE(@value1, @value2, @value3) FROM users;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_DeepNesting_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = "SELECT ISNULL(col1, ISNULL(col2, ISNULL(col3, 'default'))) FROM users;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.True(diagnostics.Length >= 1);
        Assert.All(diagnostics, d => Assert.Equal("prefer-coalesce-over-nested-isnull", d.Code));
    }

    [Fact]
    public void Metadata_HasCorrectProperties()
    {
        // Assert
        Assert.Equal("prefer-coalesce-over-nested-isnull", _rule.Metadata.RuleId);
        Assert.Equal("Style", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Information, _rule.Metadata.DefaultSeverity);
        Assert.False(_rule.Metadata.Fixable);
    }

    private static RuleContext CreateContext(string sql)
    {
        return RuleTestContext.CreateContext(sql);
    }
}
