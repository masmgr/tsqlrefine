using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Schema;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Schema;

public sealed class DuplicateColumnDefinitionRuleTests
{
    private readonly DuplicateColumnDefinitionRule _rule = new();

    [Theory]
    [InlineData("CREATE TABLE t (id INT, name VARCHAR(50), id INT);")]
    [InlineData("CREATE TABLE t (Id INT, ID INT);")]
    [InlineData("CREATE TABLE dbo.t (col1 INT, COL1 VARCHAR(10));")]
    public void Analyze_DuplicateColumnName_ReturnsDiagnostic(string sql)
    {
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("duplicate-column-definition", diagnostics[0].Code);
        Assert.Contains("defined more than once", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_TripleDuplicateColumn_ReturnsTwoDiagnostics()
    {
        const string sql = "CREATE TABLE t (id INT, name VARCHAR(50), id INT, id BIGINT);";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Equal(2, diagnostics.Length);
        Assert.All(diagnostics, d => Assert.Equal("duplicate-column-definition", d.Code));
    }

    [Theory]
    [InlineData("CREATE TABLE t (id INT, name VARCHAR(50), age INT);")]
    [InlineData("CREATE TABLE t (a INT);")]
    [InlineData(@"CREATE TABLE dbo.users (
        id INT NOT NULL,
        first_name VARCHAR(50),
        last_name VARCHAR(50)
    );")]
    public void Analyze_UniqueColumns_ReturnsEmpty(string sql)
    {
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_TempTableWithDuplicate_ReturnsDiagnostic()
    {
        const string sql = "CREATE TABLE #temp (id INT, id INT);";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
    }

    [Fact]
    public void Analyze_MultipleTables_ReportsDuplicatesIndependently()
    {
        const string sql = @"
            CREATE TABLE t1 (id INT, id INT);
            CREATE TABLE t2 (name VARCHAR(50), name VARCHAR(100));";
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
        Assert.Equal("duplicate-column-definition", _rule.Metadata.RuleId);
        Assert.Equal("Schema", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Error, _rule.Metadata.DefaultSeverity);
        Assert.False(_rule.Metadata.Fixable);
    }

    [Fact]
    public void GetFixes_ReturnsEmpty()
    {
        var context = RuleTestContext.CreateContext("CREATE TABLE t (id INT, id INT);");
        var diagnostic = new Diagnostic(
            Range: new TsqlRefine.PluginSdk.Range(new Position(0, 0), new Position(0, 10)),
            Message: "test",
            Code: "duplicate-column-definition"
        );

        var fixes = _rule.GetFixes(context, diagnostic).ToArray();

        Assert.Empty(fixes);
    }
}
