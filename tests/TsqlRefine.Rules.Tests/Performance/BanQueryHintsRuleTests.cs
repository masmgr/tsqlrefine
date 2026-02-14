using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Performance;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Performance;

public sealed class BanQueryHintsRuleTests
{
    private readonly BanQueryHintsRule _rule = new();

    [Fact]
    public void Metadata_HasCorrectProperties()
    {
        Assert.Equal("ban-query-hints", _rule.Metadata.RuleId);
        Assert.Equal("Performance", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Warning, _rule.Metadata.DefaultSeverity);
        Assert.False(_rule.Metadata.Fixable);
    }

    [Theory]
    [InlineData("SELECT id FROM dbo.Orders WITH (FORCESCAN);")]
    [InlineData("SELECT id FROM dbo.Orders WITH (FORCESEEK);")]
    [InlineData("SELECT id FROM dbo.Orders WITH (INDEX(IX_OrderDate));")]
    [InlineData("SELECT id FROM dbo.Orders WITH (NOEXPAND);")]
    public void Analyze_OptimizerForcingTableHints_ReturnsDiagnostic(string sql)
    {
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("ban-query-hints", diagnostic.Code);
    }

    [Theory]
    [InlineData("SELECT id FROM dbo.Orders WITH (UPDLOCK);")]
    [InlineData("SELECT id FROM dbo.Orders WITH (TABLOCK);")]
    [InlineData("SELECT id FROM dbo.Orders WITH (ROWLOCK);")]
    [InlineData("SELECT id FROM dbo.Orders WITH (READPAST);")]
    [InlineData("SELECT id FROM dbo.Orders WITH (READCOMMITTEDLOCK);")]
    [InlineData("SELECT id FROM dbo.Orders WITH (NOLOCK);")]
    [InlineData("SELECT id FROM dbo.Orders WITH (READUNCOMMITTED);")]
    public void Analyze_OperationalTableHints_NoDiagnostic(string sql)
    {
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Theory]
    [InlineData("SELECT id FROM dbo.Orders OPTION (FORCE ORDER);")]
    [InlineData("SELECT id FROM dbo.Orders OPTION (MAXDOP 1);")]
    [InlineData("SELECT id FROM dbo.Orders OPTION (HASH JOIN);")]
    public void Analyze_OptimizerHints_ReturnsDiagnostic(string sql)
    {
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("ban-query-hints", diagnostic.Code);
    }

    [Theory]
    [InlineData("SELECT id FROM dbo.Orders OPTION (RECOMPILE);")]
    [InlineData("WITH r AS (SELECT 1 AS n UNION ALL SELECT n + 1 FROM r WHERE n < 3) SELECT * FROM r OPTION (MAXRECURSION 100);")]
    [InlineData("DECLARE @p INT = 1; SELECT id FROM dbo.Orders WHERE id = @p OPTION (OPTIMIZE FOR (@p UNKNOWN));")]
    [InlineData("SELECT id FROM dbo.Orders OPTION (LABEL = 'reporting');")]
    public void Analyze_OperationalOptimizerHints_NoDiagnostic(string sql)
    {
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void GetFixes_ReturnsEmptyCollection()
    {
        const string sql = "SELECT id FROM dbo.Orders OPTION (FORCE ORDER);";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostic = _rule.Analyze(context).First();

        var fixes = _rule.GetFixes(context, diagnostic);

        Assert.Empty(fixes);
    }
}
