using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Style;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Style;

public sealed class PreferConcatOverPlusRuleTests
{
    private readonly PreferConcatOverPlusRule _rule = new();

    [Fact]
    public void Analyze_PlusConcatenationWithIsnull_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = "SELECT ISNULL(@firstName, '') + ' ' + @lastName FROM users;";
        var context = CreateContext(sql, compatLevel: 110);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
        Assert.Equal("prefer-concat-over-plus", diagnostics[0].Code);
        Assert.Contains("CONCAT()", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_PlusConcatenationWithCoalesce_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = "SELECT COALESCE(@firstName, '') + ' ' + @lastName FROM users;";
        var context = CreateContext(sql, compatLevel: 110);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
        Assert.Equal("prefer-concat-over-plus", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_SimplePlusConcatenation_ReturnsNoDiagnostic()
    {
        // Arrange
        const string sql = "SELECT @firstName + ' ' + @lastName FROM users;";
        var context = CreateContext(sql, compatLevel: 110);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_ConcatFunction_ReturnsNoDiagnostic()
    {
        // Arrange
        const string sql = "SELECT CONCAT(@firstName, ' ', @lastName) FROM users;";
        var context = CreateContext(sql, compatLevel: 110);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_OldCompatLevel_ReturnsNoDiagnostic()
    {
        // Arrange
        const string sql = "SELECT ISNULL(@firstName, '') + ' ' + @lastName FROM users;";
        var context = CreateContext(sql, compatLevel: 100);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Metadata_HasCorrectProperties()
    {
        // Assert
        Assert.Equal("prefer-concat-over-plus", _rule.Metadata.RuleId);
        Assert.Equal("Style", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Information, _rule.Metadata.DefaultSeverity);
        Assert.False(_rule.Metadata.Fixable);
    }

    private static RuleContext CreateContext(string sql, int compatLevel = 150)
    {
        return RuleTestContext.CreateContext(sql, compatLevel);
    }
}
