using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Schema;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Schema;

public sealed class DuplicateTableFunctionColumnRuleTests
{
    private readonly DuplicateTableFunctionColumnRule _rule = new();

    [Fact]
    public void Analyze_InlineTvfWithDuplicateColumn_ReturnsDiagnostic()
    {
        const string sql = @"
            CREATE FUNCTION dbo.fn_test()
            RETURNS TABLE
            AS
            RETURN (SELECT id, id FROM t);";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("duplicate-table-function-column", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_InlineTvfWithDuplicateAlias_ReturnsDiagnostic()
    {
        const string sql = @"
            CREATE FUNCTION dbo.fn_test()
            RETURNS TABLE
            AS
            RETURN (SELECT a AS x, b AS x FROM t);";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("duplicate-table-function-column", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_MultiStatementTvfWithDuplicateColumn_ReturnsDiagnostic()
    {
        const string sql = @"
            CREATE FUNCTION dbo.fn_test()
            RETURNS @result TABLE (id INT, id INT)
            AS
            BEGIN
                RETURN;
            END;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("duplicate-table-function-column", diagnostics[0].Code);
        Assert.Contains("id", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_MultiStatementTvfWithDuplicateCaseInsensitive_ReturnsDiagnostic()
    {
        const string sql = @"
            CREATE FUNCTION dbo.fn_test()
            RETURNS @result TABLE (Id INT, ID INT)
            AS
            BEGIN
                RETURN;
            END;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
    }

    [Fact]
    public void Analyze_InlineTvfWithUniqueColumns_ReturnsEmpty()
    {
        const string sql = @"
            CREATE FUNCTION dbo.fn_test()
            RETURNS TABLE
            AS
            RETURN (SELECT id, name FROM t);";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_MultiStatementTvfWithUniqueColumns_ReturnsEmpty()
    {
        const string sql = @"
            CREATE FUNCTION dbo.fn_test()
            RETURNS @result TABLE (id INT, name VARCHAR(50))
            AS
            BEGIN
                RETURN;
            END;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_ScalarFunction_ReturnsEmpty()
    {
        const string sql = @"
            CREATE FUNCTION dbo.fn_scalar()
            RETURNS INT
            AS
            BEGIN
                RETURN 1;
            END;";
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
        Assert.Equal("duplicate-table-function-column", _rule.Metadata.RuleId);
        Assert.Equal("Schema", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Error, _rule.Metadata.DefaultSeverity);
        Assert.False(_rule.Metadata.Fixable);
    }

    [Fact]
    public void GetFixes_ReturnsEmpty()
    {
        var context = RuleTestContext.CreateContext("SELECT 1;");
        var diagnostic = new Diagnostic(
            Range: new TsqlRefine.PluginSdk.Range(new Position(0, 0), new Position(0, 10)),
            Message: "test",
            Code: "duplicate-table-function-column"
        );

        var fixes = _rule.GetFixes(context, diagnostic).ToArray();

        Assert.Empty(fixes);
    }
}
