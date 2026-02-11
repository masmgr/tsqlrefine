using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Correctness;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Correctness;

public sealed class DuplicateInsertColumnRuleTests
{
    private readonly DuplicateInsertColumnRule _rule = new();

    [Theory]
    [InlineData("INSERT INTO t (id, name, id) VALUES (1, 'a', 2);")]
    [InlineData("INSERT INTO t (Id, ID) VALUES (1, 2);")]
    [InlineData("INSERT INTO dbo.t (col1, col1) VALUES (1, 2);")]
    public void Analyze_DuplicateColumnName_ReturnsDiagnostic(string sql)
    {
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("duplicate-insert-column", diagnostics[0].Code);
        Assert.Contains("specified more than once", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_TripleDuplicateColumn_ReturnsTwoDiagnostics()
    {
        const string sql = "INSERT INTO t (a, b, a, a) VALUES (1, 2, 3, 4);";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Equal(2, diagnostics.Length);
        Assert.All(diagnostics, d => Assert.Equal("duplicate-insert-column", d.Code));
    }

    [Fact]
    public void Analyze_InsertSelect_DuplicateColumn_ReturnsDiagnostic()
    {
        const string sql = "INSERT INTO t (id, id) SELECT 1, 2;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("duplicate-insert-column", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_MultipleInsertStatements_ReportsDuplicatesIndependently()
    {
        const string sql = @"
            INSERT INTO t1 (id, id) VALUES (1, 2);
            INSERT INTO t2 (name, name) VALUES ('a', 'b');";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Equal(2, diagnostics.Length);
    }

    [Theory]
    [InlineData("INSERT INTO t (id, name, age) VALUES (1, 'a', 20);")]
    [InlineData("INSERT INTO t VALUES (1, 2);")]
    [InlineData("INSERT INTO t (id) VALUES (1);")]
    public void Analyze_UniqueOrNoColumns_ReturnsEmpty(string sql)
    {
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
    public void Metadata_HasCorrectProperties()
    {
        Assert.Equal("duplicate-insert-column", _rule.Metadata.RuleId);
        Assert.Equal("Correctness", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Error, _rule.Metadata.DefaultSeverity);
        Assert.False(_rule.Metadata.Fixable);
    }

    [Fact]
    public void GetFixes_ReturnsEmpty()
    {
        var context = RuleTestContext.CreateContext("INSERT INTO t (id, id) VALUES (1, 2);");
        var diagnostic = new Diagnostic(
            Range: new TsqlRefine.PluginSdk.Range(new Position(0, 0), new Position(0, 10)),
            Message: "test",
            Code: "duplicate-insert-column"
        );

        var fixes = _rule.GetFixes(context, diagnostic).ToArray();

        Assert.Empty(fixes);
    }
}
