using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Safety;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Safety;

public sealed class RequireDropIfExistsRuleTests
{
    private readonly RequireDropIfExistsRule _rule = new();

    [Fact]
    public void Metadata_HasCorrectProperties()
    {
        Assert.Equal("require-drop-if-exists", _rule.Metadata.RuleId);
        Assert.Equal("Safety", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Information, _rule.Metadata.DefaultSeverity);
        Assert.False(_rule.Metadata.Fixable);
    }

    [Theory]
    [InlineData("DROP TABLE dbo.Users;")]
    [InlineData("DROP TABLE dbo.Users, dbo.Orders;")]
    public void Analyze_DropTableWithoutIfExists_ReturnsDiagnostic(string sql)
    {
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("require-drop-if-exists", diagnostics[0].Code);
        Assert.Contains("IF EXISTS", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_DropProcedureWithoutIfExists_ReturnsDiagnostic()
    {
        const string sql = "DROP PROCEDURE dbo.MyProc;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("require-drop-if-exists", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_DropViewWithoutIfExists_ReturnsDiagnostic()
    {
        const string sql = "DROP VIEW dbo.MyView;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("require-drop-if-exists", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_DropFunctionWithoutIfExists_ReturnsDiagnostic()
    {
        const string sql = "DROP FUNCTION dbo.MyFunc;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("require-drop-if-exists", diagnostics[0].Code);
    }

    [Theory]
    [InlineData("DROP TABLE dbo.Users;", 0, 10)]
    [InlineData("DROP PROCEDURE dbo.MyProc;", 0, 14)]
    [InlineData("DROP VIEW dbo.MyView;", 0, 9)]
    [InlineData("DROP FUNCTION dbo.MyFunc;", 0, 13)]
    public void Analyze_DropWithoutIfExists_HighlightsDropObjectType(string sql, int expectedStartChar, int expectedEndChar)
    {
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(0, diagnostic.Range.Start.Line);
        Assert.Equal(expectedStartChar, diagnostic.Range.Start.Character);
        Assert.Equal(0, diagnostic.Range.End.Line);
        Assert.Equal(expectedEndChar, diagnostic.Range.End.Character);
    }

    [Theory]
    [InlineData("DROP TABLE IF EXISTS dbo.Users;")]
    [InlineData("DROP PROCEDURE IF EXISTS dbo.MyProc;")]
    [InlineData("DROP VIEW IF EXISTS dbo.MyView;")]
    [InlineData("DROP FUNCTION IF EXISTS dbo.MyFunc;")]
    public void Analyze_DropWithIfExists_NoDiagnostic(string sql)
    {
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_DropTempTable_NoDiagnostic()
    {
        // Temp tables are transient - IF EXISTS is not necessary
        const string sql = "DROP TABLE #TempTable;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_MultipleDropsWithoutIfExists_ReturnsMultipleDiagnostics()
    {
        const string sql = @"
            DROP TABLE dbo.Users;
            DROP PROCEDURE dbo.MyProc;
            DROP VIEW dbo.MyView;
        ";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Equal(3, diagnostics.Length);
        Assert.All(diagnostics, d => Assert.Equal("require-drop-if-exists", d.Code));
    }

    [Theory]
    [InlineData("SELECT * FROM dbo.Users;")]
    [InlineData("CREATE TABLE dbo.Users (Id INT);")]
    [InlineData("")]
    public void Analyze_NonDropStatement_NoDiagnostic(string sql)
    {
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void GetFixes_ReturnsEmptyCollection()
    {
        const string sql = "DROP TABLE dbo.Users;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostic = _rule.Analyze(context).First();

        var fixes = _rule.GetFixes(context, diagnostic);

        Assert.Empty(fixes);
    }
}
