using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Performance;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Performance;

public sealed class DisallowSelectIntoRuleTests
{
    private readonly DisallowSelectIntoRule _rule = new();

    [Fact]
    public void Analyze_SelectInto_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = "SELECT * INTO #temp FROM users;";
        var context = RuleTestContext.CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
        Assert.Equal("avoid-select-into", diagnostics[0].Code);
        Assert.Contains("SELECT...INTO", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_SelectIntoPermanentTable_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = "SELECT id, name INTO dbo.NewUsers FROM users;";
        var context = RuleTestContext.CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
        Assert.Equal("avoid-select-into", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_SelectWithoutInto_ReturnsNoDiagnostic()
    {
        // Arrange
        const string sql = "SELECT * FROM users;";
        var context = RuleTestContext.CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_InsertIntoSelect_ReturnsNoDiagnostic()
    {
        // Arrange
        const string sql = "INSERT INTO dbo.NewUsers SELECT * FROM users;";
        var context = RuleTestContext.CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_MultipleSelectInto_ReturnsMultipleDiagnostics()
    {
        // Arrange
        const string sql = @"
            SELECT * INTO #temp1 FROM users;
            SELECT * INTO #temp2 FROM orders;
        ";
        var context = RuleTestContext.CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Equal(2, diagnostics.Length);
    }

    [Fact]
    public void Metadata_HasCorrectProperties()
    {
        // Assert
        Assert.Equal("avoid-select-into", _rule.Metadata.RuleId);
        Assert.Equal("Performance", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Information, _rule.Metadata.DefaultSeverity);
        Assert.False(_rule.Metadata.Fixable);
    }

}
