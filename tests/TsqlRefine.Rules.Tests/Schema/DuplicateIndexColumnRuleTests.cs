using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Schema;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Schema;

public sealed class DuplicateIndexColumnRuleTests
{
    private readonly DuplicateIndexColumnRule _rule = new();

    [Theory]
    [InlineData("CREATE TABLE t (a INT, b INT, INDEX IX_1 (a, b, a));")]
    [InlineData("CREATE TABLE t (a INT, b INT, INDEX IX_1 (a, A));")]
    public void Analyze_DuplicateColumnInIndex_ReturnsDiagnostic(string sql)
    {
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("duplicate-index-column", diagnostics[0].Code);
        Assert.Contains("specified more than once", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_DuplicateColumnInPrimaryKey_ReturnsDiagnostic()
    {
        const string sql = @"
            CREATE TABLE t (
                a INT,
                b INT,
                CONSTRAINT PK_t PRIMARY KEY (a, b, a)
            );";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Contains("PRIMARY KEY", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_DuplicateColumnInUniqueConstraint_ReturnsDiagnostic()
    {
        const string sql = @"
            CREATE TABLE t (
                x INT,
                y INT,
                CONSTRAINT UQ_t UNIQUE (x, y, x)
            );";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Contains("UNIQUE", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_MultipleDuplicatesInSameIndex_ReturnsMultipleDiagnostics()
    {
        const string sql = "CREATE TABLE t (a INT, b INT, INDEX IX_1 (a, b, a, b));";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Equal(2, diagnostics.Length);
    }

    [Theory]
    [InlineData("CREATE TABLE t (a INT, b INT, INDEX IX_1 (a, b));")]
    [InlineData("CREATE TABLE t (a INT, CONSTRAINT PK_t PRIMARY KEY (a));")]
    [InlineData(@"CREATE TABLE t (
        a INT,
        b INT,
        c INT,
        CONSTRAINT UQ_t UNIQUE (a, b, c)
    );")]
    public void Analyze_UniqueColumnsInIndex_ReturnsEmpty(string sql)
    {
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_MultipleIndexesWithIndependentDuplicates_ReportsEach()
    {
        const string sql = @"
            CREATE TABLE t (
                a INT,
                b INT,
                INDEX IX_1 (a, a),
                INDEX IX_2 (b, b)
            );";
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
        Assert.Equal("duplicate-index-column", _rule.Metadata.RuleId);
        Assert.Equal("Schema", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Warning, _rule.Metadata.DefaultSeverity);
        Assert.False(_rule.Metadata.Fixable);
    }

    [Fact]
    public void GetFixes_ReturnsEmpty()
    {
        var context = RuleTestContext.CreateContext("CREATE TABLE t (a INT, INDEX IX_1 (a, a));");
        var diagnostic = new Diagnostic(
            Range: new TsqlRefine.PluginSdk.Range(new Position(0, 0), new Position(0, 10)),
            Message: "test",
            Code: "duplicate-index-column"
        );

        var fixes = _rule.GetFixes(context, diagnostic).ToArray();

        Assert.Empty(fixes);
    }
}
