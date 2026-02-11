using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Schema;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Schema;

public sealed class DuplicateTableVariableColumnRuleTests
{
    private readonly DuplicateTableVariableColumnRule _rule = new();

    [Theory]
    [InlineData("DECLARE @t TABLE (id INT, id INT);")]
    [InlineData("DECLARE @t TABLE (Id INT, ID INT);")]
    [InlineData("DECLARE @t TABLE (col1 VARCHAR(50), COL1 INT);")]
    public void Analyze_DuplicateColumnName_ReturnsDiagnostic(string sql)
    {
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("duplicate-table-variable-column", diagnostics[0].Code);
        Assert.Contains("defined more than once", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_TripleDuplicateColumn_ReturnsTwoDiagnostics()
    {
        const string sql = "DECLARE @t TABLE (id INT, name VARCHAR(50), id INT, id BIGINT);";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Equal(2, diagnostics.Length);
        Assert.All(diagnostics, d => Assert.Equal("duplicate-table-variable-column", d.Code));
    }

    [Theory]
    [InlineData("DECLARE @t TABLE (id INT, name VARCHAR(50));")]
    [InlineData("DECLARE @t TABLE (a INT);")]
    [InlineData("DECLARE @t TABLE (id INT, name VARCHAR(50), age INT);")]
    public void Analyze_UniqueColumns_ReturnsEmpty(string sql)
    {
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_MultipleTableVariables_ReportsDuplicatesIndependently()
    {
        const string sql = @"
            DECLARE @t1 TABLE (id INT, id INT);
            DECLARE @t2 TABLE (name VARCHAR(50), name VARCHAR(100));";
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
        Assert.Equal("duplicate-table-variable-column", _rule.Metadata.RuleId);
        Assert.Equal("Schema", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Error, _rule.Metadata.DefaultSeverity);
        Assert.False(_rule.Metadata.Fixable);
    }

    [Fact]
    public void GetFixes_ReturnsEmpty()
    {
        var context = RuleTestContext.CreateContext("DECLARE @t TABLE (id INT, id INT);");
        var diagnostic = new Diagnostic(
            Range: new TsqlRefine.PluginSdk.Range(new Position(0, 0), new Position(0, 10)),
            Message: "test",
            Code: "duplicate-table-variable-column"
        );

        var fixes = _rule.GetFixes(context, diagnostic).ToArray();

        Assert.Empty(fixes);
    }
}
