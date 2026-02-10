using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Correctness;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Correctness;

public sealed class DuplicateSelectColumnRuleTests
{
    private readonly DuplicateSelectColumnRule _rule = new();

    [Theory]
    [InlineData("SELECT id, id FROM t;")]
    [InlineData("SELECT Id, ID FROM t;")]
    [InlineData("SELECT t.id, t.id FROM t;")]
    public void Analyze_DuplicateColumnName_ReturnsDiagnostic(string sql)
    {
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("duplicate-select-column", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_DuplicateAlias_ReturnsDiagnostic()
    {
        const string sql = "SELECT a AS x, b AS x FROM t;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("duplicate-select-column", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_QualifiedColumnsSameBaseName_ReturnsDiagnostic()
    {
        const string sql = "SELECT t.id, s.id FROM t INNER JOIN s ON t.id = s.id;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("duplicate-select-column", diagnostics[0].Code);
    }

    [Theory]
    [InlineData("SELECT id, name FROM t;")]
    [InlineData("SELECT a AS x, b AS y FROM t;")]
    [InlineData("SELECT t.id, s.name FROM t INNER JOIN s ON t.id = s.id;")]
    public void Analyze_UniqueColumns_ReturnsEmpty(string sql)
    {
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_SelectStar_ReturnsEmpty()
    {
        const string sql = "SELECT * FROM t;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_SubqueryDuplicateAlsoDetected()
    {
        const string sql = "SELECT a, b FROM (SELECT id, id FROM t) sub;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        // The subquery has duplicate 'id' columns
        Assert.Single(diagnostics);
        Assert.Equal("duplicate-select-column", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_MultipleDuplicates_ReportsAll()
    {
        const string sql = "SELECT id, name, id, name FROM t;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Equal(2, diagnostics.Length);
        Assert.All(diagnostics, d => Assert.Equal("duplicate-select-column", d.Code));
    }

    [Fact]
    public void Analyze_MultipleStatements_ReportsDuplicatesIndependently()
    {
        const string sql = @"
            SELECT id, id FROM t;
            SELECT name, name FROM s;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Equal(2, diagnostics.Length);
    }

    [Fact]
    public void Analyze_ExpressionWithoutAlias_ReturnsEmpty()
    {
        // Expressions without aliases can't be name-checked
        const string sql = "SELECT id, id + 1 FROM t;";
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
        Assert.Equal("duplicate-select-column", _rule.Metadata.RuleId);
        Assert.Equal("Correctness", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Warning, _rule.Metadata.DefaultSeverity);
        Assert.False(_rule.Metadata.Fixable);
    }

    [Fact]
    public void GetFixes_ReturnsEmpty()
    {
        var context = RuleTestContext.CreateContext("SELECT id, id FROM t;");
        var diagnostic = new Diagnostic(
            Range: new TsqlRefine.PluginSdk.Range(new Position(0, 0), new Position(0, 10)),
            Message: "test",
            Code: "duplicate-select-column"
        );

        var fixes = _rule.GetFixes(context, diagnostic).ToArray();

        Assert.Empty(fixes);
    }
}
