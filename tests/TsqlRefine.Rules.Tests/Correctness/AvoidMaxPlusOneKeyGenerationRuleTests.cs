using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Correctness;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Correctness;

public sealed class AvoidMaxPlusOneKeyGenerationRuleTests
{
    private readonly AvoidMaxPlusOneKeyGenerationRule _rule = new();

    [Theory]
    [InlineData("SET @next = (SELECT MAX(Id) + 1 FROM dbo.Items);")]
    [InlineData("SET @next = (SELECT MAX(Id) + (1) FROM dbo.Items);")]
    [InlineData("SELECT @next = ISNULL(MAX(Id), 0) + 1 FROM dbo.Items;")]
    [InlineData("SELECT @next = 1 + COALESCE(MAX(Id), 0) FROM dbo.Items;")]
    [InlineData("UPDATE dbo.State SET NextId = (SELECT CAST(MAX(Id) AS bigint) + 10 FROM dbo.Items);")]
    [InlineData("INSERT INTO dbo.State (NextId) SELECT MAX(Id) + 1 FROM dbo.Items;")]
    [InlineData("INSERT INTO dbo.State (NextId) VALUES ((SELECT MAX(Id) + 1 FROM dbo.Items));")]
    public void Analyze_StateChangingMaxPlusPositiveInteger_ReturnsDiagnostic(string sql)
    {
        var diagnostics = _rule.Analyze(RuleTestContext.CreateContext(sql)).ToArray();

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("avoid-max-plus-one-key-generation", diagnostic.Code);
    }

    [Theory]
    [InlineData("SELECT MAX(Id) + 1 AS SuggestedValue FROM dbo.Items;")]
    [InlineData("SET @maximum = (SELECT MAX(Id) FROM dbo.Items);")]
    [InlineData("SET @next = @current + 1;")]
    [InlineData("SET @next = (SELECT MAX(Id) + @step FROM dbo.Items);")]
    [InlineData("SET @next = (SELECT MAX(Id) + 0 FROM dbo.Items);")]
    public void Analyze_NonAllocationPatterns_ReturnsEmpty(string sql)
    {
        var diagnostics = _rule.Analyze(RuleTestContext.CreateContext(sql)).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Metadata_HasExpectedValues()
    {
        Assert.Equal("Correctness", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Warning, _rule.Metadata.DefaultSeverity);
        Assert.False(_rule.Metadata.Fixable);
    }
}
