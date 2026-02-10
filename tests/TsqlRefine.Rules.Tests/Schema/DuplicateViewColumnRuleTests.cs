using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Schema;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Schema;

public sealed class DuplicateViewColumnRuleTests
{
    private readonly DuplicateViewColumnRule _rule = new();

    [Theory]
    [InlineData("CREATE VIEW v AS SELECT id, id FROM t;")]
    [InlineData("CREATE VIEW v AS SELECT Id, ID FROM t;")]
    [InlineData("CREATE VIEW dbo.v AS SELECT col1, col1 FROM t;")]
    public void Analyze_DuplicateColumnInSelect_ReturnsDiagnostic(string sql)
    {
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("duplicate-view-column", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_DuplicateAliasInSelect_ReturnsDiagnostic()
    {
        const string sql = "CREATE VIEW v AS SELECT a AS x, b AS x FROM t;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("duplicate-view-column", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_ExplicitColumnListWithDuplicate_ReturnsDiagnostic()
    {
        const string sql = "CREATE VIEW v (col1, col1) AS SELECT a, b FROM t;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("duplicate-view-column", diagnostics[0].Code);
        Assert.Contains("col1", diagnostics[0].Message);
    }

    [Theory]
    [InlineData("CREATE VIEW v AS SELECT id, name FROM t;")]
    [InlineData("CREATE VIEW v AS SELECT a AS x, b AS y FROM t;")]
    [InlineData("CREATE VIEW v (col1, col2) AS SELECT a, b FROM t;")]
    public void Analyze_UniqueColumns_ReturnsEmpty(string sql)
    {
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_SelectStarInView_ReturnsEmpty()
    {
        const string sql = "CREATE VIEW v AS SELECT * FROM t;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_MultipleViews_ReportsDuplicatesIndependently()
    {
        const string sql = @"
            CREATE VIEW v1 AS SELECT id, id FROM t;
            GO
            CREATE VIEW v2 AS SELECT name, name FROM t;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Equal(2, diagnostics.Length);
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
        Assert.Equal("duplicate-view-column", _rule.Metadata.RuleId);
        Assert.Equal("Schema", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Error, _rule.Metadata.DefaultSeverity);
        Assert.False(_rule.Metadata.Fixable);
    }

    [Fact]
    public void GetFixes_ReturnsEmpty()
    {
        var context = RuleTestContext.CreateContext("CREATE VIEW v AS SELECT id, id FROM t;");
        var diagnostic = new Diagnostic(
            Range: new TsqlRefine.PluginSdk.Range(new Position(0, 0), new Position(0, 10)),
            Message: "test",
            Code: "duplicate-view-column"
        );

        var fixes = _rule.GetFixes(context, diagnostic).ToArray();

        Assert.Empty(fixes);
    }
}
