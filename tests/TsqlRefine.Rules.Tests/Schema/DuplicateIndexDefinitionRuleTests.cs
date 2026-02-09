using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Schema;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Schema;

public sealed class DuplicateIndexDefinitionRuleTests
{
    private readonly DuplicateIndexDefinitionRule _rule = new();

    [Fact]
    public void Analyze_TwoIndexesSameColumns_ReturnsDiagnostic()
    {
        const string sql = @"
            CREATE TABLE t (
                a INT,
                b INT,
                INDEX IX_1 (a, b),
                INDEX IX_2 (a, b)
            );";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("duplicate-index-definition", diagnostics[0].Code);
        Assert.Contains("IX_1", diagnostics[0].Message);
        Assert.Contains("IX_2", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_IndexAndUniqueSameColumns_ReturnsDiagnostic()
    {
        const string sql = @"
            CREATE TABLE t (
                a INT,
                b INT,
                INDEX IX_1 (a, b),
                CONSTRAINT UQ_1 UNIQUE (a, b)
            );";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Contains("same column composition", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_PrimaryKeyAndIndexSameColumns_ReturnsDiagnostic()
    {
        const string sql = @"
            CREATE TABLE t (
                a INT,
                CONSTRAINT PK_t PRIMARY KEY (a),
                INDEX IX_1 (a)
            );";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
    }

    [Fact]
    public void Analyze_CaseInsensitiveColumnNames_ReturnsDiagnostic()
    {
        const string sql = @"
            CREATE TABLE t (
                Col1 INT,
                Col2 INT,
                INDEX IX_1 (Col1, Col2),
                INDEX IX_2 (COL1, COL2)
            );";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
    }

    [Fact]
    public void Analyze_ThreeIdenticalIndexes_ReturnsTwoDiagnostics()
    {
        const string sql = @"
            CREATE TABLE t (
                a INT,
                INDEX IX_1 (a),
                INDEX IX_2 (a),
                INDEX IX_3 (a)
            );";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Equal(2, diagnostics.Length);
    }

    [Fact]
    public void Analyze_DifferentColumnOrder_ReturnsEmpty()
    {
        const string sql = @"
            CREATE TABLE t (
                a INT,
                b INT,
                INDEX IX_1 (a, b),
                INDEX IX_2 (b, a)
            );";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_DifferentSortOrder_ReturnsEmpty()
    {
        const string sql = @"
            CREATE TABLE t (
                a INT,
                b INT,
                INDEX IX_1 (a ASC, b ASC),
                INDEX IX_2 (a ASC, b DESC)
            );";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_DifferentColumns_ReturnsEmpty()
    {
        const string sql = @"
            CREATE TABLE t (
                a INT,
                b INT,
                c INT,
                INDEX IX_1 (a, b),
                INDEX IX_2 (a, c)
            );";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_SingleIndex_ReturnsEmpty()
    {
        const string sql = "CREATE TABLE t (a INT, INDEX IX_1 (a));";
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
        Assert.Equal("duplicate-index-definition", _rule.Metadata.RuleId);
        Assert.Equal("Schema", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Warning, _rule.Metadata.DefaultSeverity);
        Assert.False(_rule.Metadata.Fixable);
    }

    [Fact]
    public void GetFixes_ReturnsEmpty()
    {
        var context = RuleTestContext.CreateContext("CREATE TABLE t (a INT, INDEX IX_1 (a), INDEX IX_2 (a));");
        var diagnostic = new Diagnostic(
            Range: new TsqlRefine.PluginSdk.Range(new Position(0, 0), new Position(0, 10)),
            Message: "test",
            Code: "duplicate-index-definition"
        );

        var fixes = _rule.GetFixes(context, diagnostic).ToArray();

        Assert.Empty(fixes);
    }
}
