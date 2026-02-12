using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Correctness;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Correctness;

public sealed class GroupByColumnMismatchRuleTests
{
    private readonly GroupByColumnMismatchRule _rule = new();

    // === Violation cases ===

    [Fact]
    public void Analyze_ColumnNotInGroupBy_ReturnsDiagnostic()
    {
        const string sql = "SELECT a, b FROM t GROUP BY a;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("group-by-column-mismatch", diagnostics[0].Code);
        Assert.Contains("b", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_QualifiedColumnNotInGroupBy_ReturnsDiagnostic()
    {
        const string sql = "SELECT t.a, t.b FROM t GROUP BY t.a;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Contains("t.b", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_MultipleColumnsNotInGroupBy_ReportsAll()
    {
        const string sql = "SELECT a, b, c FROM t GROUP BY a;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Equal(2, diagnostics.Length);
        Assert.All(diagnostics, d => Assert.Equal("group-by-column-mismatch", d.Code));
    }

    [Fact]
    public void Analyze_ColumnInExpressionNotInGroupBy_ReturnsDiagnostic()
    {
        const string sql = "SELECT a, b + 1 FROM t GROUP BY a;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Contains("b", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_ColumnInCaseNotInGroupBy_ReturnsDiagnostic()
    {
        const string sql = "SELECT a, CASE WHEN b > 0 THEN b ELSE 0 END FROM t GROUP BY a;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.True(diagnostics.Length >= 1);
        Assert.All(diagnostics, d => Assert.Contains("b", d.Message));
    }

    [Fact]
    public void Analyze_ColumnInCastNotInGroupBy_ReturnsDiagnostic()
    {
        const string sql = "SELECT a, CAST(b AS varchar(10)) FROM t GROUP BY a;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Contains("b", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_ColumnInNonAggregateFunction_ReturnsDiagnostic()
    {
        const string sql = "SELECT a, UPPER(b) FROM t GROUP BY a;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Contains("b", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_MixedQualifiedAndUnqualified_MatchesCorrectly()
    {
        // GROUP BY uses unqualified 'a', SELECT uses qualified 't.a' — should match
        const string sql = "SELECT t.a, b FROM t GROUP BY a;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        // Only 'b' should be flagged, not 't.a'
        Assert.Single(diagnostics);
        Assert.Contains("b", diagnostics[0].Message);
    }

    // === Valid cases (no violations) ===

    [Fact]
    public void Analyze_AllColumnsInGroupBy_ReturnsEmpty()
    {
        const string sql = "SELECT a, b FROM t GROUP BY a, b;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_GroupByExpression_SelectsSameExpression_ReturnsEmpty()
    {
        const string sql = "SELECT a + b FROM t GROUP BY a + b;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_GroupByExpression_WithUngroupedColumn_ReturnsDiagnostic()
    {
        const string sql = "SELECT a + b, c FROM t GROUP BY a + b;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Contains("c", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_ColumnInAggregateFunction_ReturnsEmpty()
    {
        const string sql = "SELECT a, COUNT(b) FROM t GROUP BY a;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Theory]
    [InlineData("SELECT a, SUM(b) FROM t GROUP BY a;")]
    [InlineData("SELECT a, AVG(b) FROM t GROUP BY a;")]
    [InlineData("SELECT a, MIN(b) FROM t GROUP BY a;")]
    [InlineData("SELECT a, MAX(b) FROM t GROUP BY a;")]
    [InlineData("SELECT a, COUNT_BIG(b) FROM t GROUP BY a;")]
    public void Analyze_VariousAggregateFunctions_ReturnsEmpty(string sql)
    {
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_ConstantLiteral_ReturnsEmpty()
    {
        const string sql = "SELECT a, 1 FROM t GROUP BY a;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_StringLiteral_ReturnsEmpty()
    {
        const string sql = "SELECT a, 'hello' FROM t GROUP BY a;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_NoGroupBy_ReturnsEmpty()
    {
        const string sql = "SELECT a, b FROM t;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_SelectStar_ReturnsEmpty()
    {
        const string sql = "SELECT * FROM t GROUP BY a;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_ScalarSubquery_ReturnsEmpty()
    {
        const string sql = "SELECT a, (SELECT MAX(c) FROM s) FROM t GROUP BY a;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_WindowFunction_ReturnsEmpty()
    {
        const string sql = "SELECT a, ROW_NUMBER() OVER(ORDER BY b) FROM t GROUP BY a;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_CountStarWithAlias_ReturnsEmpty()
    {
        const string sql = "SELECT a, COUNT(*) AS cnt FROM t GROUP BY a;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_AggregateWithExpression_ReturnsEmpty()
    {
        const string sql = "SELECT a, SUM(b * c) FROM t GROUP BY a;";
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
    public void Analyze_SubqueryGroupByIndependent()
    {
        // Outer query has no GROUP BY; subquery has GROUP BY with violation
        const string sql = @"
            SELECT x FROM (
                SELECT a, b FROM t GROUP BY a
            ) sub;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        // Only the subquery's 'b' should be flagged
        Assert.Single(diagnostics);
        Assert.Contains("b", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_CoalesceWithGroupByColumn_ReturnsEmpty()
    {
        const string sql = "SELECT a, COALESCE(a, 'N/A') FROM t GROUP BY a;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_CoalesceWithNonGroupByColumn_ReturnsDiagnostic()
    {
        const string sql = "SELECT a, COALESCE(b, 'N/A') FROM t GROUP BY a;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Contains("b", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_MultipleStatements_ReportsIndependently()
    {
        const string sql = @"
            SELECT a, b FROM t GROUP BY a;
            SELECT x, y FROM s GROUP BY x;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Equal(2, diagnostics.Length);
    }

    [Fact]
    public void Analyze_ColumnInAggregateInsideCaseExpression_ReturnsEmpty()
    {
        const string sql = "SELECT a, CASE WHEN COUNT(b) > 0 THEN 1 ELSE 0 END FROM t GROUP BY a;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    // === Metadata tests ===

    [Fact]
    public void Metadata_HasCorrectProperties()
    {
        Assert.Equal("group-by-column-mismatch", _rule.Metadata.RuleId);
        Assert.Equal("Correctness", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Warning, _rule.Metadata.DefaultSeverity);
        Assert.False(_rule.Metadata.Fixable);
    }

    [Fact]
    public void GetFixes_ReturnsEmpty()
    {
        var context = RuleTestContext.CreateContext("SELECT a, b FROM t GROUP BY a;");
        var diagnostic = new Diagnostic(
            Range: new TsqlRefine.PluginSdk.Range(new Position(0, 0), new Position(0, 10)),
            Message: "test",
            Code: "group-by-column-mismatch"
        );

        var fixes = _rule.GetFixes(context, diagnostic).ToArray();

        Assert.Empty(fixes);
    }
}
