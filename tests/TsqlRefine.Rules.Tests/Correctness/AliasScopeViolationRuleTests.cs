using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Correctness.Semantic;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Correctness;

public sealed class AliasScopeViolationRuleTests
{
    [Theory]
    [InlineData("SELECT * FROM (SELECT * FROM t1 WHERE t2.id = 1) x JOIN t2 ON 1=1")]  // Inner references t2 before defined
    [InlineData("SELECT * FROM (SELECT t3.col FROM t1) x, t2, t3")]  // Subquery references t3 from outer scope (order issue)
    [InlineData("SELECT * FROM (SELECT t2.col FROM t1) x JOIN t2 ON x.col = t2.id")]  // Derived table forward-references t2 in JOIN
    [InlineData("SELECT * FROM t1, (SELECT t3.id FROM t2) x, t3")]  // Middle derived table forward-references t3 at end
    [InlineData("SELECT * FROM (SELECT y.id FROM t1) x JOIN (SELECT 1 AS id) y ON x.id = y.id")]  // Forward-reference to later derived table alias
    public void Analyze_WhenAliasScopeViolation_ReturnsDiagnostic(string sql)
    {
        var rule = new AliasScopeViolationRule();
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.NotEmpty(diagnostics);
        Assert.Contains(diagnostics, d => d.Data?.RuleId == "semantic/alias-scope-violation");
        Assert.All(diagnostics.Where(d => d.Data?.RuleId == "semantic/alias-scope-violation"), d =>
        {
            Assert.Equal("Correctness", d.Data?.Category);
            Assert.False(d.Data?.Fixable);
        });
    }

    [Theory]
    [InlineData("SELECT * FROM t1 WHERE EXISTS (SELECT 1 FROM t2 WHERE t2.id = t1.id)")]  // Valid correlation
    [InlineData("SELECT * FROM (SELECT * FROM t1) x JOIN t2 ON x.id = t2.id")]  // Valid reference
    [InlineData("SELECT * FROM t1, (SELECT * FROM t2) x WHERE t1.id = x.id")]  // Valid reference
    [InlineData("SELECT * FROM t1 WHERE t1.id IN (SELECT t2.id FROM t2)")]  // Valid subquery
    [InlineData("SELECT * FROM (SELECT * FROM t1 WHERE t1.id = 1) x")]  // Self-reference in subquery
    [InlineData("SELECT * FROM t1, (SELECT t1.col FROM t1 AS t1inner) x")]  // Derived table references t1 defined before it
    [InlineData("SELECT * FROM t1 a JOIN (SELECT a.id FROM t2) x ON 1=1")]  // Derived table references alias 'a' defined before it in JOIN
    [InlineData("SELECT * FROM t1, t2, (SELECT t1.col, t2.col FROM t3) x")]  // Derived table references t1, t2 both defined before it
    [InlineData("SELECT * FROM t1 LEFT JOIN (SELECT * FROM t2) x ON t1.id = x.id")]  // Derived table in LEFT JOIN, no forward ref
    [InlineData("SELECT * FROM (SELECT 1 AS id) x")]  // Derived table with no external references at all
    [InlineData("SELECT * FROM (SELECT t1.id FROM t1 WHERE EXISTS (SELECT 1 WHERE t1.id = 1)) x, t1")]  // Local alias inside derived table should not be treated as outer forward reference
    public void Analyze_WhenNoAliasScopeViolation_ReturnsEmpty(string sql)
    {
        var rule = new AliasScopeViolationRule();
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).Where(d => d.Data?.RuleId == "semantic/alias-scope-violation").ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_CorrelatedSubquery_NoViolation()
    {
        var rule = new AliasScopeViolationRule();
        // This is a valid correlated subquery - outer table referenced in inner WHERE
        var sql = "SELECT * FROM orders o WHERE EXISTS (SELECT 1 FROM order_items oi WHERE oi.order_id = o.id)";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).Where(d => d.Data?.RuleId == "semantic/alias-scope-violation").ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_EmptyInput_ReturnsEmpty()
    {
        var rule = new AliasScopeViolationRule();
        var context = RuleTestContext.CreateContext("");

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void GetFixes_ReturnsEmpty()
    {
        var rule = new AliasScopeViolationRule();
        var context = RuleTestContext.CreateContext("SELECT * FROM (SELECT * FROM t1 WHERE t2.id = 1) x JOIN t2 ON 1=1");
        var diagnostic = new Diagnostic(
            Range: new TsqlRefine.PluginSdk.Range(new Position(0, 0), new Position(0, 10)),
            Message: "test",
            Code: "semantic/alias-scope-violation"
        );

        var fixes = rule.GetFixes(context, diagnostic).ToArray();

        Assert.Empty(fixes);
    }

    [Fact]
    public void Metadata_HasCorrectValues()
    {
        var rule = new AliasScopeViolationRule();

        Assert.Equal("semantic/alias-scope-violation", rule.Metadata.RuleId);
        Assert.Equal("Correctness", rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Warning, rule.Metadata.DefaultSeverity);
        Assert.False(rule.Metadata.Fixable);
        Assert.Contains("scope", rule.Metadata.Description, StringComparison.OrdinalIgnoreCase);
    }


}
