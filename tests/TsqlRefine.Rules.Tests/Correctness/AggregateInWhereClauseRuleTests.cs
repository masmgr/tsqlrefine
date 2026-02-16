using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Correctness;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Correctness;

public sealed class AggregateInWhereClauseRuleTests
{
    private readonly AggregateInWhereClauseRule _rule = new();

    // === Violation cases ===

    [Fact]
    public void Analyze_CountStarInWhere_ReturnsDiagnostic()
    {
        const string sql = "SELECT * FROM t WHERE COUNT(*) > 5;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("aggregate-in-where-clause", diagnostics[0].Code);
        Assert.Contains("COUNT", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_SumInWhereComparison_ReturnsDiagnostic()
    {
        const string sql = "SELECT * FROM t WHERE SUM(amount) > 100;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Contains("SUM", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_AvgInWhereWithAnd_ReturnsDiagnostic()
    {
        const string sql = "SELECT * FROM t WHERE x = 1 AND AVG(price) > 50;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Contains("AVG", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_MultipleAggregatesInWhere_ReportsAll()
    {
        const string sql = "SELECT * FROM t WHERE COUNT(*) > 0 AND SUM(x) > 10;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Equal(2, diagnostics.Length);
    }

    [Fact]
    public void Analyze_AggregateInBetween_ReturnsDiagnostic()
    {
        const string sql = "SELECT * FROM t WHERE MIN(x) BETWEEN 1 AND 10;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Contains("MIN", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_MaxInWhereEquality_ReturnsDiagnostic()
    {
        const string sql = "SELECT * FROM t WHERE MAX(price) = 100;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Contains("MAX", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_CountBigInWhere_ReturnsDiagnostic()
    {
        const string sql = "SELECT * FROM t WHERE COUNT_BIG(*) > 1000000;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Contains("COUNT_BIG", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_AggregateInIsNull_ReturnsDiagnostic()
    {
        const string sql = "SELECT * FROM t WHERE SUM(x) IS NULL;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Contains("SUM", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_AggregateInLikePredicate_ReturnsDiagnostic()
    {
        const string sql = "SELECT * FROM t WHERE MIN(name) LIKE 'A%';";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Contains("MIN", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_AggregateInNestedParentheses_ReturnsDiagnostic()
    {
        const string sql = "SELECT * FROM t WHERE (COUNT(*) > 5);";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Contains("COUNT", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_AggregateInInPredicate_ReturnsDiagnostic()
    {
        const string sql = "SELECT * FROM t WHERE MAX(x) IN (1, 2, 3);";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Contains("MAX", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_AggregateInCaseInWhere_ReturnsDiagnostic()
    {
        const string sql = "SELECT * FROM t WHERE CASE WHEN COUNT(*) > 0 THEN 1 ELSE 0 END = 1;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Contains("COUNT", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_AggregateInTryCastInWhere_ReturnsDiagnostic()
    {
        const string sql = "SELECT * FROM t WHERE TRY_CAST(COUNT(*) AS int) > 0;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Contains("COUNT", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_AggregateInSubqueryComparisonExpression_ReturnsDiagnostic()
    {
        const string sql = "SELECT * FROM t WHERE COUNT(*) > ALL (SELECT 1);";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Contains("COUNT", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_AggregateWindowFunctionInWhere_ReturnsDiagnostic()
    {
        const string sql = "SELECT * FROM t WHERE SUM(x) OVER (PARTITION BY y) > 0;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Contains("SUM", diagnostics[0].Message);
    }

    // === Valid cases (no violations) ===

    [Fact]
    public void Analyze_AggregateInSubqueryInWhere_ReturnsEmpty()
    {
        const string sql = "SELECT * FROM t WHERE x > (SELECT COUNT(*) FROM s);";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_AggregateInExistsSubquery_ReturnsEmpty()
    {
        const string sql = "SELECT * FROM t WHERE EXISTS (SELECT 1 FROM s HAVING COUNT(*) > 0);";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_NoWhereClause_ReturnsEmpty()
    {
        const string sql = "SELECT * FROM t;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_WhereWithoutAggregates_ReturnsEmpty()
    {
        const string sql = "SELECT * FROM t WHERE x > 5 AND y = 'abc';";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_AggregateInHaving_ReturnsEmpty()
    {
        const string sql = "SELECT a, COUNT(*) FROM t GROUP BY a HAVING COUNT(*) > 5;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_MaxInSubqueryComparison_ReturnsEmpty()
    {
        const string sql = "SELECT * FROM orders WHERE price > (SELECT MAX(price) FROM products);";
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
    public void Analyze_AggregateInSelectOnly_ReturnsEmpty()
    {
        const string sql = "SELECT COUNT(*) FROM t;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_ScalarFunctionInWhere_ReturnsEmpty()
    {
        const string sql = "SELECT * FROM t WHERE UPPER(name) = 'JOHN';";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_AggregateInInSubquery_ReturnsEmpty()
    {
        const string sql = "SELECT * FROM t WHERE x IN (SELECT MAX(y) FROM s);";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_MultipleStatements_ReportsIndependently()
    {
        const string sql = @"
            SELECT * FROM t WHERE COUNT(*) > 5;
            SELECT * FROM s WHERE x > 0;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
    }

    // === Scalar subquery cases (no false positives) ===

    [Fact]
    public void Analyze_AggregateInScalarSubqueryWhereReferencingOuterGroupBy_ReturnsEmpty()
    {
        const string sql = @"
            SELECT
                (
                    SELECT TOP 1 t2.code
                    FROM t2
                    WHERE t2.project = MAX(t1.project)
                ) AS code
            FROM t1
            GROUP BY t1.category;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_AggregateInScalarSubqueryWhereWithinOuterWhere_ReturnsEmpty()
    {
        const string sql = @"
            SELECT col
            FROM t1
            WHERE x > (
                SELECT TOP 1 val
                FROM t2
                WHERE t2.y = MAX(t1.z)
            )
            GROUP BY col, x;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_AggregateInOuterWhereWithSubquery_ReturnsDiagnostic()
    {
        const string sql = @"
            SELECT *
            FROM t
            WHERE COUNT(*) > 5
                AND x IN (SELECT y FROM s);";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Contains("COUNT", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_AggregateInNestedScalarSubqueryWhere_ReturnsEmpty()
    {
        const string sql = @"
            SELECT
                (
                    SELECT TOP 1
                        (SELECT TOP 1 a.val FROM a WHERE a.id = MAX(t.id))
                    FROM b
                    WHERE b.code = MIN(t.code)
                ) AS nested_val
            FROM t
            GROUP BY t.category;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_MixedAggregatesOuterAndSubquery_ReportsOnlyOuter()
    {
        const string sql = @"
            SELECT
                (SELECT TOP 1 s.val FROM s WHERE s.id = MAX(t.id)) AS sub_val
            FROM t
            WHERE SUM(t.amount) > 100
            GROUP BY t.category;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Contains("SUM", diagnostics[0].Message);
    }

    // === Metadata tests ===

    [Fact]
    public void Metadata_HasCorrectProperties()
    {
        Assert.Equal("aggregate-in-where-clause", _rule.Metadata.RuleId);
        Assert.Equal("Correctness", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Error, _rule.Metadata.DefaultSeverity);
        Assert.False(_rule.Metadata.Fixable);
    }

    [Fact]
    public void GetFixes_ReturnsEmpty()
    {
        var context = RuleTestContext.CreateContext("SELECT * FROM t WHERE COUNT(*) > 5;");
        var diagnostic = new Diagnostic(
            Range: new TsqlRefine.PluginSdk.Range(new Position(0, 0), new Position(0, 10)),
            Message: "test",
            Code: "aggregate-in-where-clause"
        );

        var fixes = _rule.GetFixes(context, diagnostic).ToArray();

        Assert.Empty(fixes);
    }
}
