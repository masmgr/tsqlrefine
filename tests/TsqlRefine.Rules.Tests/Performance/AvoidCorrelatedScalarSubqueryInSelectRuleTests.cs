using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Performance;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Performance;

public sealed class AvoidCorrelatedScalarSubqueryInSelectRuleTests
{
    private readonly AvoidCorrelatedScalarSubqueryInSelectRule _rule = new();

    [Fact]
    public void Metadata_HasCorrectProperties()
    {
        Assert.Equal("avoid-correlated-scalar-subquery-in-select", _rule.Metadata.RuleId);
        Assert.Equal("Performance", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Warning, _rule.Metadata.DefaultSeverity);
        Assert.False(_rule.Metadata.Fixable);
    }

    [Fact]
    public void Analyze_CorrelatedSubqueryInSelect_ReturnsDiagnostic()
    {
        const string sql = "SELECT t1.col, (SELECT t2.val FROM t2 WHERE t2.id = t1.id) AS x FROM t1;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("avoid-correlated-scalar-subquery-in-select", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_CorrelatedSubqueryWithAliases_ReturnsDiagnostic()
    {
        const string sql = "SELECT a.col, (SELECT MAX(b.val) FROM t2 b WHERE b.id = a.id) AS x FROM t1 a;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
    }

    [Fact]
    public void Analyze_MultipleCorrelatedSubqueries_ReturnsMultipleDiagnostics()
    {
        const string sql = """
            SELECT
                t1.col,
                (SELECT t2.val FROM t2 WHERE t2.id = t1.id) AS x,
                (SELECT t3.val FROM t3 WHERE t3.id = t1.id) AS y
            FROM t1;
            """;
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Equal(2, diagnostics.Length);
    }

    [Fact]
    public void Analyze_CorrelatedSubqueryInsideIsnull_ReturnsDiagnostic()
    {
        const string sql = "SELECT ISNULL((SELECT t2.val FROM t2 WHERE t2.id = t1.id), 0) FROM t1;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
    }

    [Fact]
    public void Analyze_CorrelatedSubqueryWithJoinCorrelation_ReturnsDiagnostic()
    {
        const string sql = "SELECT t1.col, (SELECT t2.val FROM t2 WHERE t2.fk = t1.pk) FROM t1;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
    }

    [Fact]
    public void Analyze_NonCorrelatedSubquery_NoDiagnostic()
    {
        const string sql = "SELECT t1.col, (SELECT MAX(id) FROM t2) AS x FROM t1;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_CorrelatedSubqueryInWhere_NoDiagnostic()
    {
        const string sql = "SELECT * FROM t1 WHERE t1.col = (SELECT MAX(id) FROM t2 WHERE t2.fk = t1.pk);";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_ExistsSubquery_NoDiagnostic()
    {
        const string sql = "SELECT * FROM t1 WHERE EXISTS (SELECT 1 FROM t2 WHERE t2.id = t1.id);";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_DerivedTable_NoDiagnostic()
    {
        const string sql = "SELECT * FROM t1 JOIN (SELECT id, MAX(val) AS mx FROM t2 GROUP BY id) AS sub ON sub.id = t1.id;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_NoSubquery_NoDiagnostic()
    {
        const string sql = "SELECT col FROM t1;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_NonCorrelatedSubqueryWithSameColumnNames_NoDiagnostic()
    {
        const string sql = "SELECT t1.col, (SELECT MAX(id) FROM t2 WHERE t2.status = 'active') FROM t1;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void GetFixes_ReturnsEmptyCollection()
    {
        const string sql = "SELECT t1.col, (SELECT t2.val FROM t2 WHERE t2.id = t1.id) AS x FROM t1;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostic = _rule.Analyze(context).First();
        var fixes = _rule.GetFixes(context, diagnostic);

        Assert.Empty(fixes);
    }
}
