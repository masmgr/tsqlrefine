using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Correctness;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Correctness;

public sealed class InsertColumnCountMismatchRuleTests
{
    [Theory]
    [InlineData("INSERT INTO t (a, b) SELECT x, y, z FROM t2")]  // 3 cols vs 2
    [InlineData("INSERT INTO t (a, b, c) VALUES (1, 2)")]  // 2 values vs 3 cols
    [InlineData("INSERT INTO t (a) VALUES (1, 2)")]  // 2 values vs 1 col
    [InlineData("INSERT INTO t (a, b) SELECT 1, 2, 3")]  // 3 values vs 2 cols
    [InlineData("INSERT INTO t (col1, col2, col3, col4) VALUES (1, 2, 3)")]  // 3 values vs 4 cols
    [InlineData("INSERT INTO t (x) SELECT a, b FROM t2")]  // 2 cols vs 1 col
    public void Analyze_WhenColumnCountMismatch_ReturnsDiagnostic(string sql)
    {
        var rule = new InsertColumnCountMismatchRule();
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.NotEmpty(diagnostics);
        Assert.Contains(diagnostics, d => d.Data?.RuleId == "semantic/insert-column-count-mismatch");
        Assert.All(diagnostics.Where(d => d.Data?.RuleId == "semantic/insert-column-count-mismatch"), d =>
        {
            Assert.Equal("Correctness", d.Data?.Category);
            Assert.False(d.Data?.Fixable);
            Assert.Contains("column count", d.Message, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Theory]
    [InlineData("INSERT INTO t (a, b) SELECT x, y FROM t2")]  // match
    [InlineData("INSERT INTO t (a, b) VALUES (1, 2)")]  // match
    [InlineData("INSERT INTO t SELECT * FROM t2")]  // no column list, can't verify
    [InlineData("INSERT INTO t VALUES (1, 2, 3)")]  // no column list, can't verify
    [InlineData("INSERT INTO t (a) SELECT x FROM t2")]  // single column match
    [InlineData("INSERT INTO t (a, b, c) VALUES (1, 2, 3)")]  // 3 cols match
    [InlineData("INSERT INTO t (a, b, c) SELECT 1, 2, 3")]  // 3 cols match
    public void Analyze_WhenColumnCountMatches_ReturnsEmpty(string sql)
    {
        var rule = new InsertColumnCountMismatchRule();
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).Where(d => d.Data?.RuleId == "semantic/insert-column-count-mismatch").ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_MultipleRowsValues_AllRowsMustMatch()
    {
        var rule = new InsertColumnCountMismatchRule();
        // First row has 2 values, target has 3 columns
        var sql = "INSERT INTO t (a, b, c) VALUES (1, 2), (3, 4)";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).Where(d => d.Data?.RuleId == "semantic/insert-column-count-mismatch").ToArray();

        Assert.NotEmpty(diagnostics);
    }

    [Fact]
    public void Analyze_ValuesWithMultipleRows_AllMatch_ReturnsEmpty()
    {
        var rule = new InsertColumnCountMismatchRule();
        var sql = "INSERT INTO t (a, b) VALUES (1, 2), (3, 4), (5, 6)";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).Where(d => d.Data?.RuleId == "semantic/insert-column-count-mismatch").ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_SelectWithSubquery_CountsSelectElements()
    {
        var rule = new InsertColumnCountMismatchRule();
        var sql = "INSERT INTO t (a, b) SELECT (SELECT x FROM t2), y, z FROM t3";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).Where(d => d.Data?.RuleId == "semantic/insert-column-count-mismatch").ToArray();

        // 3 select elements vs 2 columns
        Assert.NotEmpty(diagnostics);
    }

    [Fact]
    public void Analyze_EmptyInput_ReturnsEmpty()
    {
        var rule = new InsertColumnCountMismatchRule();
        var context = RuleTestContext.CreateContext("");

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void GetFixes_ReturnsEmpty()
    {
        var rule = new InsertColumnCountMismatchRule();
        var context = RuleTestContext.CreateContext("INSERT INTO t (a, b) VALUES (1, 2, 3)");
        var diagnostic = new Diagnostic(
            Range: new TsqlRefine.PluginSdk.Range(new Position(0, 0), new Position(0, 10)),
            Message: "test",
            Code: "semantic/insert-column-count-mismatch"
        );

        var fixes = rule.GetFixes(context, diagnostic).ToArray();

        Assert.Empty(fixes);
    }

    [Fact]
    public void Metadata_HasCorrectValues()
    {
        var rule = new InsertColumnCountMismatchRule();

        Assert.Equal("semantic/insert-column-count-mismatch", rule.Metadata.RuleId);
        Assert.Equal("Correctness", rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Error, rule.Metadata.DefaultSeverity);
        Assert.False(rule.Metadata.Fixable);
        Assert.Contains("column count", rule.Metadata.Description, StringComparison.OrdinalIgnoreCase);
    }


}
