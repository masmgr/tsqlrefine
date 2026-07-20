using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Correctness;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Correctness;

public sealed class RequireSemicolonBeforeThrowRuleTests
{
    private readonly RequireSemicolonBeforeThrowRule _rule = new();

    [Theory]
    [InlineData("PRINT 'failed'\nTHROW;")]
    [InlineData("SELECT 1\nTHROW 50000, 'failed', 1;")]
    [InlineData("BEGIN TRY SELECT 1; END TRY BEGIN CATCH ROLLBACK TRANSACTION\nTHROW; END CATCH")]
    [InlineData("BEGIN TRY SELECT 1; END TRY BEGIN CATCH ROLLBACK TRANSACTION THROW; END CATCH")]
    public void Analyze_PrecedingStatementWithoutSemicolon_ReturnsDiagnostic(string sql)
    {
        var diagnostics = _rule.Analyze(RuleTestContext.CreateContext(sql)).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("require-semicolon-before-throw", diagnostics[0].Code);
        Assert.Contains("semicolon", diagnostics[0].Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("PRINT 'failed';\nTHROW;")]
    [InlineData("BEGIN CATCH THROW; END CATCH")]
    [InlineData("IF @failed = 1 THROW 50000, 'failed', 1;")]
    [InlineData("THROW;")]
    [InlineData("")]
    public void Analyze_WhenSemicolonIsNotRequiredOrIsPresent_ReturnsEmpty(string sql)
    {
        var diagnostics = _rule.Analyze(RuleTestContext.CreateContext(sql)).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Metadata_HasCorrectProperties()
    {
        Assert.Equal("Correctness", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Error, _rule.Metadata.DefaultSeverity);
        Assert.False(_rule.Metadata.Fixable);
    }
}
