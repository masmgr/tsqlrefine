using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Performance;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Performance;

public sealed class AvoidOrOnDifferentColumnsRuleTests
{
    private readonly AvoidOrOnDifferentColumnsRule _rule = new();

    [Fact]
    public void Metadata_HasCorrectProperties()
    {
        Assert.Equal("avoid-or-on-different-columns", _rule.Metadata.RuleId);
        Assert.Equal("Performance", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Information, _rule.Metadata.DefaultSeverity);
        Assert.False(_rule.Metadata.Fixable);
    }

    [Fact]
    public void Analyze_DifferentColumnsInWhere_ReturnsDiagnostic()
    {
        const string sql = "SELECT * FROM t WHERE colA = @x OR colB = @y;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("avoid-or-on-different-columns", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_QualifiedDifferentColumns_ReturnsDiagnostic()
    {
        const string sql = "SELECT * FROM t WHERE t.colA = 1 OR t.colB = 2;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
    }

    [Fact]
    public void Analyze_DifferentColumnsInHaving_ReturnsDiagnostic()
    {
        const string sql = "SELECT colA, colB, COUNT(*) FROM t GROUP BY colA, colB HAVING colA > 0 OR colB > 0;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
    }

    [Fact]
    public void Analyze_DifferentColumnsInJoinOn_ReturnsDiagnostic()
    {
        const string sql = "SELECT * FROM t1 JOIN t2 ON t1.id = t2.id OR t1.code = t2.name;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
    }

    [Fact]
    public void Analyze_ParenthesizedComparisons_ReturnsDiagnostic()
    {
        const string sql = "SELECT * FROM t WHERE (colA = @x) OR (colB = @y);";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
    }

    [Theory]
    [InlineData("SELECT * FROM t WHERE col = 1 OR col = 2;")]
    [InlineData("SELECT * FROM t WHERE t.col = 1 OR t.col = 2;")]
    [InlineData("SELECT * FROM t WHERE colA = @x OR colA = @y;")]
    public void Analyze_SameColumn_NoDiagnostic(string sql)
    {
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_SameBaseNameDifferentQualifiers_NoDiagnostic()
    {
        const string sql = "SELECT * FROM t1 JOIN t2 ON 1 = 1 WHERE t1.col = 1 OR t2.col = 2;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_LeftSideIsAnd_NoDiagnostic()
    {
        const string sql = "SELECT * FROM t WHERE (colA = 1 AND colB > 0) OR colC = 2;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_RightSideIsAnd_NoDiagnostic()
    {
        const string sql = "SELECT * FROM t WHERE colA = 1 OR (colB = 2 AND colC = 3);";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_NoColumnReferences_NoDiagnostic()
    {
        const string sql = "SELECT * FROM t WHERE 1 = 1 OR 2 = 2;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_NoPredicate_NoDiagnostic()
    {
        const string sql = "SELECT colA, colB FROM t;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void GetFixes_ReturnsEmptyCollection()
    {
        const string sql = "SELECT * FROM t WHERE colA = @x OR colB = @y;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostic = _rule.Analyze(context).First();
        var fixes = _rule.GetFixes(context, diagnostic);

        Assert.Empty(fixes);
    }
}
