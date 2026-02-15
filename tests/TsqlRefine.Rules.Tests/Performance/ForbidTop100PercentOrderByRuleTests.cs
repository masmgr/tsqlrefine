using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Performance;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Performance;

public sealed class ForbidTop100PercentOrderByRuleTests
{
    private readonly ForbidTop100PercentOrderByRule _rule = new();

    [Fact]
    public void Analyze_Top100PercentWithOrderBy_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = "SELECT TOP 100 PERCENT * FROM users ORDER BY id;";
        var context = RuleTestContext.CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
        Assert.Equal("avoid-top-100-percent-order-by", diagnostics[0].Code);
        Assert.Contains("TOP 100 PERCENT", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_Top100PercentWithoutOrderBy_ReturnsNoDiagnostic()
    {
        // Arrange
        const string sql = "SELECT TOP 100 PERCENT * FROM users;";
        var context = RuleTestContext.CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_Top50PercentWithOrderBy_ReturnsNoDiagnostic()
    {
        // Arrange
        const string sql = "SELECT TOP 50 PERCENT * FROM users ORDER BY id;";
        var context = RuleTestContext.CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_TopWithoutPercent_ReturnsNoDiagnostic()
    {
        // Arrange
        const string sql = "SELECT TOP 100 * FROM users ORDER BY id;";
        var context = RuleTestContext.CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_OrderByWithoutTop_ReturnsNoDiagnostic()
    {
        // Arrange
        const string sql = "SELECT * FROM users ORDER BY id;";
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
        Assert.Equal("avoid-top-100-percent-order-by", _rule.Metadata.RuleId);
        Assert.Equal("Performance", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Warning, _rule.Metadata.DefaultSeverity);
        Assert.False(_rule.Metadata.Fixable);
    }

}
