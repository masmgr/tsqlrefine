using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Correctness;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Correctness;

public sealed class HavingColumnMismatchRuleTests
{
    private readonly HavingColumnMismatchRule _rule = new();

    // === Violation cases ===

    [Fact]
    public void Analyze_ColumnInHavingNotInGroupBy_ReturnsDiagnostic()
    {
        const string sql = "SELECT a FROM t GROUP BY a HAVING b > 5;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("having-column-mismatch", diagnostics[0].Code);
        Assert.Contains("b", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_MultipleColumnsInHavingNotInGroupBy_ReportsAll()
    {
        const string sql = "SELECT a FROM t GROUP BY a HAVING b > 5 AND c < 10;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Equal(2, diagnostics.Length);
        Assert.All(diagnostics, d => Assert.Equal("having-column-mismatch", d.Code));
    }

    [Fact]
    public void Analyze_QualifiedColumnInHavingNotInGroupBy_ReturnsDiagnostic()
    {
        const string sql = "SELECT t.a FROM t GROUP BY t.a HAVING t.b > 5;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Contains("t.b", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_ColumnInHavingComparisonNotInGroupBy_ReturnsDiagnostic()
    {
        const string sql = "SELECT a, COUNT(*) FROM t GROUP BY a HAVING b = 1;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Contains("b", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_ColumnInHavingBetweenNotInGroupBy_ReturnsDiagnostic()
    {
        const string sql = "SELECT a FROM t GROUP BY a HAVING b BETWEEN 1 AND 10;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Contains("b", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_ColumnInHavingIsNullNotInGroupBy_ReturnsDiagnostic()
    {
        const string sql = "SELECT a FROM t GROUP BY a HAVING b IS NULL;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Contains("b", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_ColumnInHavingLikeNotInGroupBy_ReturnsDiagnostic()
    {
        const string sql = "SELECT a FROM t GROUP BY a HAVING b LIKE 'x%';";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Contains("b", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_ColumnInHavingInPredicateNotInGroupBy_ReturnsDiagnostic()
    {
        const string sql = "SELECT a FROM t GROUP BY a HAVING b IN (1, 2, 3);";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Contains("b", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_MixedQualifiedUnqualified_MatchesCorrectly()
    {
        // GROUP BY uses unqualified 'a', HAVING uses qualified 't.b' — should only flag b
        const string sql = "SELECT t.a FROM t GROUP BY a HAVING t.b > 5;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Contains("b", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_ColumnInNonAggregateFunctionInHaving_ReturnsDiagnostic()
    {
        const string sql = "SELECT a FROM t GROUP BY a HAVING UPPER(b) = 'X';";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Contains("b", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_GroupByExpression_HavingUngroupedColumn_ReturnsDiagnostic()
    {
        const string sql = "SELECT a + b FROM t GROUP BY a + b HAVING c > 0;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Contains("c", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_RepeatedUngroupedColumnInHavingExpression_ReportsOnce()
    {
        const string sql = "SELECT a FROM t GROUP BY a HAVING CASE WHEN b > 0 THEN b ELSE b END > 0;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Contains("b", diagnostics[0].Message);
    }

    // === Valid cases (no violations) ===

    [Fact]
    public void Analyze_AllHavingColumnsInGroupBy_ReturnsEmpty()
    {
        const string sql = "SELECT a, b FROM t GROUP BY a, b HAVING a > 5;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_AggregatesInHaving_ReturnsEmpty()
    {
        const string sql = "SELECT a, COUNT(*) FROM t GROUP BY a HAVING COUNT(*) > 5;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_HavingWithGroupByColumnComparison_ReturnsEmpty()
    {
        const string sql = "SELECT a FROM t GROUP BY a HAVING a > 5;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_GroupByExpression_HavingSameExpression_ReturnsEmpty()
    {
        const string sql = "SELECT a + b FROM t GROUP BY a + b HAVING a + b > 0;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_NoHavingClause_ReturnsEmpty()
    {
        const string sql = "SELECT a, COUNT(*) FROM t GROUP BY a;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_NoGroupByNoHaving_ReturnsEmpty()
    {
        const string sql = "SELECT * FROM t;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_HavingWithOnlyAggregates_ReturnsEmpty()
    {
        const string sql = "SELECT a, SUM(b) FROM t GROUP BY a HAVING SUM(b) > 100;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_HavingWithRollupGroupByColumn_ReturnsEmpty()
    {
        const string sql = "SELECT a, COUNT(*) FROM t GROUP BY ROLLUP(a) HAVING a IS NOT NULL;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_HavingWithCubeGroupByColumns_ReturnsEmpty()
    {
        const string sql = "SELECT a, b, COUNT(*) FROM t GROUP BY CUBE(a, b) HAVING a IS NOT NULL OR b IS NOT NULL;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_HavingWithGroupingSetsGroupByColumns_ReturnsEmpty()
    {
        const string sql = "SELECT a, b, COUNT(*) FROM t GROUP BY GROUPING SETS ((a, b), (a), ()) HAVING a IS NOT NULL OR b IS NOT NULL;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_HavingWithRollupAndUngroupedColumn_ReturnsDiagnostic()
    {
        const string sql = "SELECT a, COUNT(*) FROM t GROUP BY ROLLUP(a) HAVING c > 0;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Contains("c", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_HavingWithSubquery_ReturnsEmpty()
    {
        const string sql = "SELECT a FROM t GROUP BY a HAVING a > (SELECT MAX(x) FROM s);";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_EmptyInput_ReturnsEmpty()
    {
        var context = RuleTestContext.CreateContext("");

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_MultipleStatements_ReportsIndependently()
    {
        const string sql = @"
            SELECT a FROM t GROUP BY a HAVING b > 5;
            SELECT x FROM s GROUP BY x HAVING y < 10;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Equal(2, diagnostics.Length);
    }

    [Fact]
    public void Analyze_NoGroupByWithHaving_ReturnsEmpty()
    {
        // GROUP BY なしの HAVING は別のエラー（8121）— このルールのスコープ外
        const string sql = "SELECT COUNT(*) FROM t HAVING COUNT(*) > 5;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_HavingWithMultipleAggregates_ReturnsEmpty()
    {
        const string sql = "SELECT a, COUNT(*), AVG(b) FROM t GROUP BY a HAVING COUNT(*) > 5 AND AVG(b) > 10;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_GroupByColumnInHavingExpression_ReturnsEmpty()
    {
        const string sql = "SELECT a FROM t GROUP BY a HAVING a + 0 > 5;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_SubqueryHavingIndependent()
    {
        // Outer has no HAVING; subquery has HAVING with violation
        const string sql = @"
            SELECT x FROM (
                SELECT a, b FROM t GROUP BY a HAVING b > 5
            ) sub;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        // Only the subquery's 'b' should be flagged
        Assert.Single(diagnostics);
        Assert.Contains("b", diagnostics[0].Message);
    }

    // === Metadata tests ===

    [Fact]
    public void Metadata_HasCorrectProperties()
    {
        Assert.Equal("having-column-mismatch", _rule.Metadata.RuleId);
        Assert.Equal("Correctness", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Warning, _rule.Metadata.DefaultSeverity);
        Assert.False(_rule.Metadata.Fixable);
    }

    [Fact]
    public void GetFixes_ReturnsEmpty()
    {
        var context = RuleTestContext.CreateContext("SELECT a FROM t GROUP BY a HAVING b > 5;");
        var diagnostic = new Diagnostic(
            Range: new TsqlRefine.PluginSdk.Range(new Position(0, 0), new Position(0, 10)),
            Message: "test",
            Code: "having-column-mismatch"
        );

        var fixes = _rule.GetFixes(context, diagnostic).ToArray();

        Assert.Empty(fixes);
    }
}
