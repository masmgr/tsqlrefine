using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Schema;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Schema;

public sealed class DuplicateForeignKeyColumnRuleTests
{
    private readonly DuplicateForeignKeyColumnRule _rule = new();

    [Fact]
    public void Analyze_DuplicateColumnInForeignKey_ReturnsDiagnostic()
    {
        const string sql = @"
            CREATE TABLE t (
                a INT,
                b INT,
                c INT,
                FOREIGN KEY (a, b, a) REFERENCES other_table (x, y, z)
            );";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("duplicate-foreign-key-column", diagnostics[0].Code);
        Assert.Contains("specified more than once", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_CaseInsensitiveDuplicate_ReturnsDiagnostic()
    {
        const string sql = @"
            CREATE TABLE t (
                Col1 INT,
                Col2 INT,
                FOREIGN KEY (Col1, COL1) REFERENCES other_table (x, y)
            );";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
    }

    [Fact]
    public void Analyze_NamedConstraintWithDuplicate_ReturnsMessageWithName()
    {
        const string sql = @"
            CREATE TABLE t (
                a INT,
                b INT,
                CONSTRAINT FK_t_other FOREIGN KEY (a, b, a) REFERENCES other_table (x, y, z)
            );";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Contains("FK_t_other", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_MultipleForeignKeysWithIndependentDuplicates_ReportsEach()
    {
        const string sql = @"
            CREATE TABLE t (
                a INT,
                b INT,
                FOREIGN KEY (a, a) REFERENCES t1 (x, y),
                FOREIGN KEY (b, b) REFERENCES t2 (x, y)
            );";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Equal(2, diagnostics.Length);
    }

    [Fact]
    public void Analyze_UniqueColumnsInForeignKey_ReturnsEmpty()
    {
        const string sql = @"
            CREATE TABLE t (
                a INT,
                b INT,
                FOREIGN KEY (a, b) REFERENCES other_table (x, y)
            );";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_SingleColumnForeignKey_ReturnsEmpty()
    {
        const string sql = @"
            CREATE TABLE t (
                a INT,
                FOREIGN KEY (a) REFERENCES other_table (x)
            );";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_NoForeignKeys_ReturnsEmpty()
    {
        const string sql = "CREATE TABLE t (a INT, b INT);";
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
        Assert.Equal("duplicate-foreign-key-column", _rule.Metadata.RuleId);
        Assert.Equal("Schema", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Warning, _rule.Metadata.DefaultSeverity);
        Assert.False(_rule.Metadata.Fixable);
    }

    [Fact]
    public void GetFixes_ReturnsEmpty()
    {
        var context = RuleTestContext.CreateContext("CREATE TABLE t (a INT, FOREIGN KEY (a, a) REFERENCES other_table (x, y));");
        var diagnostic = new Diagnostic(
            Range: new TsqlRefine.PluginSdk.Range(new Position(0, 0), new Position(0, 10)),
            Message: "test",
            Code: "duplicate-foreign-key-column"
        );

        var fixes = _rule.GetFixes(context, diagnostic).ToArray();

        Assert.Empty(fixes);
    }
}
