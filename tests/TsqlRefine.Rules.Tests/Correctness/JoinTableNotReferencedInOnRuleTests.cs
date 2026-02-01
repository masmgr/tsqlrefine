using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Correctness.Semantic;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Correctness;

public sealed class JoinTableNotReferencedInOnRuleTests
{
    private const string RuleId = "semantic/join-table-not-referenced-in-on";

    [Theory]
    [InlineData("SELECT * FROM t1 INNER JOIN t2 ON t1.id = 1")]  // t2 not referenced
    [InlineData("SELECT * FROM t1 LEFT JOIN t2 ON t1.status = 'active'")]  // t2 not referenced
    [InlineData("SELECT * FROM t1 RIGHT JOIN t2 ON t1.col = t1.col")]  // t2 not referenced (self-ref on t1)
    [InlineData("SELECT * FROM t1 JOIN t2 ON t1.a = t1.b")]  // t2 not referenced
    [InlineData("SELECT * FROM t1 a INNER JOIN t2 b ON a.id = 1")]  // alias b not referenced
    [InlineData("SELECT * FROM t1 LEFT OUTER JOIN t2 ON t1.x = t1.y")]  // LEFT OUTER, t2 not referenced
    [InlineData("SELECT * FROM t1 RIGHT OUTER JOIN t2 ON t1.x = 1")]  // RIGHT OUTER, t2 not referenced
    public void Analyze_WhenJoinedTableNotReferenced_ReturnsDiagnostic(string sql)
    {
        var rule = new JoinTableNotReferencedInOnRule();
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.NotEmpty(diagnostics);
        Assert.Contains(diagnostics, d => d.Data?.RuleId == RuleId);
        Assert.All(diagnostics.Where(d => d.Data?.RuleId == RuleId), d =>
        {
            Assert.Equal("Correctness", d.Data?.Category);
            Assert.False(d.Data?.Fixable);
        });
    }

    [Theory]
    [InlineData("SELECT * FROM t1 INNER JOIN t2 ON t1.id = t2.id")]  // both referenced
    [InlineData("SELECT * FROM t1 LEFT JOIN t2 ON t1.id = t2.fk AND t2.status = 1")]  // t2 referenced
    [InlineData("SELECT * FROM t1 a JOIN t2 b ON a.id = b.id")]  // both aliases referenced
    [InlineData("SELECT * FROM t1 RIGHT JOIN t2 ON t2.id = t1.id")]  // t2 referenced
    [InlineData("SELECT * FROM t1 CROSS JOIN t2")]  // CROSS JOIN (no ON clause, uses UnqualifiedJoin)
    [InlineData("SELECT * FROM t1, t2 WHERE t1.id = t2.id")]  // comma join (no QualifiedJoin)
    [InlineData("SELECT * FROM t1 FULL OUTER JOIN t2 ON t1.id = t2.id")]  // FULL OUTER excluded from check
    [InlineData("SELECT * FROM t1 LEFT JOIN t2 ON t2.col IS NOT NULL")]  // t2 referenced via IS NULL
    [InlineData("SELECT * FROM t1 JOIN t2 ON t2.status IN (1, 2, 3)")]  // t2 referenced in IN clause
    public void Analyze_WhenJoinedTableReferenced_ReturnsEmpty(string sql)
    {
        var rule = new JoinTableNotReferencedInOnRule();
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).Where(d => d.Data?.RuleId == RuleId).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_MultipleJoins_ChecksEachIndividually()
    {
        var rule = new JoinTableNotReferencedInOnRule();
        // Both t2 and t3 are not referenced in their respective ON clauses
        var sql = "SELECT * FROM t1 JOIN t2 ON t1.a = 1 JOIN t3 ON t1.b = 2";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).Where(d => d.Data?.RuleId == RuleId).ToArray();

        Assert.Equal(2, diagnostics.Length);
    }

    [Fact]
    public void Analyze_MixedJoins_OnlyReportsUnreferencedTables()
    {
        var rule = new JoinTableNotReferencedInOnRule();
        // t2 is referenced, t3 is not
        var sql = "SELECT * FROM t1 JOIN t2 ON t1.id = t2.id JOIN t3 ON t1.x = 1";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).Where(d => d.Data?.RuleId == RuleId).ToArray();

        Assert.Single(diagnostics);
        Assert.Contains("t3", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_DerivedTableWithAlias_ChecksAliasReference()
    {
        var rule = new JoinTableNotReferencedInOnRule();
        // Derived table with alias 'sub' not referenced in ON clause
        var sql = "SELECT * FROM t1 JOIN (SELECT id FROM t2) sub ON t1.x = 1";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).Where(d => d.Data?.RuleId == RuleId).ToArray();

        Assert.Single(diagnostics);
        Assert.Contains("sub", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_DerivedTableReferenced_NoWarning()
    {
        var rule = new JoinTableNotReferencedInOnRule();
        // Derived table with alias 'sub' is referenced
        var sql = "SELECT * FROM t1 JOIN (SELECT id FROM t2) sub ON t1.id = sub.id";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).Where(d => d.Data?.RuleId == RuleId).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_SubqueryInOnClause_DoesNotTraverse()
    {
        var rule = new JoinTableNotReferencedInOnRule();
        // The subquery references t2, but the outer t2 column is not directly referenced
        var sql = "SELECT * FROM t1 JOIN t2 ON t1.id = (SELECT MAX(id) FROM t2)";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).Where(d => d.Data?.RuleId == RuleId).ToArray();

        // Should emit diagnostic because outer t2 is not directly referenced in ON clause
        Assert.Single(diagnostics);
        Assert.Contains("t2", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_FullOuterJoin_NotChecked()
    {
        var rule = new JoinTableNotReferencedInOnRule();
        // FULL OUTER JOIN with t2 not referenced - should not trigger (not in target join types)
        var sql = "SELECT * FROM t1 FULL OUTER JOIN t2 ON t1.id = 1";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).Where(d => d.Data?.RuleId == RuleId).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_EmptyInput_ReturnsEmpty()
    {
        var rule = new JoinTableNotReferencedInOnRule();
        var context = RuleTestContext.CreateContext("");

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void GetFixes_ReturnsEmpty()
    {
        var rule = new JoinTableNotReferencedInOnRule();
        var context = RuleTestContext.CreateContext("SELECT * FROM t1 JOIN t2 ON t1.id = 1");
        var diagnostic = new Diagnostic(
            Range: new TsqlRefine.PluginSdk.Range(new Position(0, 0), new Position(0, 10)),
            Message: "test",
            Code: RuleId
        );

        var fixes = rule.GetFixes(context, diagnostic).ToArray();

        Assert.Empty(fixes);
    }

    [Fact]
    public void Metadata_HasCorrectValues()
    {
        var rule = new JoinTableNotReferencedInOnRule();

        Assert.Equal(RuleId, rule.Metadata.RuleId);
        Assert.Equal("Correctness", rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Warning, rule.Metadata.DefaultSeverity);
        Assert.False(rule.Metadata.Fixable);
        Assert.Contains("JOIN", rule.Metadata.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ON clause", rule.Metadata.Description, StringComparison.OrdinalIgnoreCase);
    }
}
