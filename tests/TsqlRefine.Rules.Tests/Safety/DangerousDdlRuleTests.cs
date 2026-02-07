using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Safety;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Safety;

public sealed class DangerousDdlRuleTests
{
    private readonly DangerousDdlRule _rule = new();

    [Fact]
    public void Metadata_HasCorrectProperties()
    {
        Assert.Equal("dangerous-ddl", _rule.Metadata.RuleId);
        Assert.Equal("Safety", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Warning, _rule.Metadata.DefaultSeverity);
        Assert.False(_rule.Metadata.Fixable);
    }

    // --- DROP DATABASE ---

    [Fact]
    public void Analyze_DropDatabase_ReturnsDiagnostic()
    {
        const string sql = "DROP DATABASE ProductionDB;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        var d = Assert.Single(diagnostics);
        Assert.Equal("dangerous-ddl", d.Code);
        Assert.Contains("DROP DATABASE", d.Message);
    }

    // --- DROP TABLE ---

    [Fact]
    public void Analyze_DropTable_ReturnsDiagnostic()
    {
        const string sql = "DROP TABLE dbo.Orders;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        var d = Assert.Single(diagnostics);
        Assert.Equal("dangerous-ddl", d.Code);
        Assert.Contains("DROP TABLE", d.Message);
        Assert.Null(d.Severity); // Uses default severity (Warning)
    }

    [Fact]
    public void Analyze_DropTableIfExists_ReturnsDiagnosticWithLowerSeverity()
    {
        const string sql = "DROP TABLE IF EXISTS dbo.Orders;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        var d = Assert.Single(diagnostics);
        Assert.Equal("dangerous-ddl", d.Code);
        Assert.Contains("DROP TABLE", d.Message);
        Assert.Equal(DiagnosticSeverity.Information, d.Severity);
    }

    [Fact]
    public void Analyze_DropTempTable_NoDiagnostic()
    {
        const string sql = "DROP TABLE #TempResults;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_DropGlobalTempTable_NoDiagnostic()
    {
        const string sql = "DROP TABLE ##GlobalTemp;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    // --- DROP PROCEDURE ---

    [Fact]
    public void Analyze_DropProcedure_ReturnsDiagnostic()
    {
        const string sql = "DROP PROCEDURE dbo.MyProc;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        var d = Assert.Single(diagnostics);
        Assert.Equal("dangerous-ddl", d.Code);
        Assert.Null(d.Severity);
    }

    [Fact]
    public void Analyze_DropProcedureIfExists_ReturnsDiagnosticWithLowerSeverity()
    {
        const string sql = "DROP PROCEDURE IF EXISTS dbo.MyProc;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        var d = Assert.Single(diagnostics);
        Assert.Equal("dangerous-ddl", d.Code);
        Assert.Equal(DiagnosticSeverity.Information, d.Severity);
    }

    // --- DROP VIEW ---

    [Fact]
    public void Analyze_DropView_ReturnsDiagnostic()
    {
        const string sql = "DROP VIEW dbo.MyView;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        var d = Assert.Single(diagnostics);
        Assert.Equal("dangerous-ddl", d.Code);
        Assert.Null(d.Severity);
    }

    [Fact]
    public void Analyze_DropViewIfExists_ReturnsDiagnosticWithLowerSeverity()
    {
        const string sql = "DROP VIEW IF EXISTS dbo.MyView;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        var d = Assert.Single(diagnostics);
        Assert.Equal("dangerous-ddl", d.Code);
        Assert.Equal(DiagnosticSeverity.Information, d.Severity);
    }

    // --- DROP FUNCTION ---

    [Fact]
    public void Analyze_DropFunction_ReturnsDiagnostic()
    {
        const string sql = "DROP FUNCTION dbo.MyFunc;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        var d = Assert.Single(diagnostics);
        Assert.Equal("dangerous-ddl", d.Code);
        Assert.Null(d.Severity);
    }

    [Fact]
    public void Analyze_DropFunctionIfExists_ReturnsDiagnosticWithLowerSeverity()
    {
        const string sql = "DROP FUNCTION IF EXISTS dbo.MyFunc;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        var d = Assert.Single(diagnostics);
        Assert.Equal("dangerous-ddl", d.Code);
        Assert.Equal(DiagnosticSeverity.Information, d.Severity);
    }

    // --- TRUNCATE TABLE ---

    [Fact]
    public void Analyze_TruncateTable_ReturnsDiagnostic()
    {
        const string sql = "TRUNCATE TABLE dbo.Logs;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        var d = Assert.Single(diagnostics);
        Assert.Equal("dangerous-ddl", d.Code);
        Assert.Contains("TRUNCATE", d.Message);
    }

    // --- ALTER TABLE DROP ---

    [Fact]
    public void Analyze_AlterTableDropColumn_ReturnsDiagnostic()
    {
        const string sql = "ALTER TABLE dbo.Users DROP COLUMN EmailAddress;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        var d = Assert.Single(diagnostics);
        Assert.Equal("dangerous-ddl", d.Code);
        Assert.Contains("ALTER TABLE DROP", d.Message);
    }

    [Fact]
    public void Analyze_AlterTableDropConstraint_ReturnsDiagnostic()
    {
        const string sql = "ALTER TABLE dbo.Orders DROP CONSTRAINT FK_Orders_Customers;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        var d = Assert.Single(diagnostics);
        Assert.Equal("dangerous-ddl", d.Code);
    }

    // --- Multiple statements ---

    [Fact]
    public void Analyze_MultipleDropStatements_ReturnsMultipleDiagnostics()
    {
        const string sql = @"
            DROP TABLE dbo.Orders;
            DROP VIEW dbo.MyView;
            TRUNCATE TABLE dbo.Logs;
        ";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Equal(3, diagnostics.Length);
        Assert.All(diagnostics, d => Assert.Equal("dangerous-ddl", d.Code));
    }

    // --- Negative cases ---

    [Theory]
    [InlineData("SELECT * FROM Users;")]
    [InlineData("INSERT INTO Users (Name) VALUES ('test');")]
    [InlineData("CREATE TABLE dbo.NewTable (Id INT);")]
    [InlineData("ALTER TABLE dbo.Users ADD NewColumn INT;")]
    [InlineData("")]
    public void Analyze_NonDestructiveDdl_NoDiagnostic(string sql)
    {
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void GetFixes_ReturnsEmptyCollection()
    {
        const string sql = "DROP TABLE dbo.Orders;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostic = _rule.Analyze(context).First();

        var fixes = _rule.GetFixes(context, diagnostic);

        Assert.Empty(fixes);
    }
}
