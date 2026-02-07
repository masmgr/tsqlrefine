using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Security;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Security;

public sealed class AvoidExecuteAsRuleTests
{
    private readonly AvoidExecuteAsRule _rule = new();

    [Fact]
    public void Metadata_HasCorrectProperties()
    {
        Assert.Equal("avoid-execute-as", _rule.Metadata.RuleId);
        Assert.Equal("Security", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Warning, _rule.Metadata.DefaultSeverity);
        Assert.False(_rule.Metadata.Fixable);
    }

    [Theory]
    [InlineData("EXECUTE AS USER = 'dbo';")]
    [InlineData("EXECUTE AS LOGIN = 'sa';")]
    [InlineData("EXEC AS USER = 'dbo';")]
    public void Analyze_ExecuteAsStatement_ReturnsDiagnostic(string sql)
    {
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("avoid-execute-as", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_ExecuteAsOwnerInProc_ReturnsDiagnostic()
    {
        const string sql = @"
            CREATE PROCEDURE dbo.MyProc
            WITH EXECUTE AS OWNER
            AS
            BEGIN
                SELECT 1;
            END;
        ";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("avoid-execute-as", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_ExecuteAsSelfInProc_ReturnsDiagnostic()
    {
        const string sql = @"
            CREATE PROCEDURE dbo.MyProc
            WITH EXECUTE AS SELF
            AS
            BEGIN
                SELECT 1;
            END;
        ";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("avoid-execute-as", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_ExecuteAsCallerInProc_NoDiagnostic()
    {
        // EXECUTE AS CALLER is the default and does not escalate privileges
        const string sql = @"
            CREATE PROCEDURE dbo.MyProc
            WITH EXECUTE AS CALLER
            AS
            BEGIN
                SELECT 1;
            END;
        ";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_ExecuteAsInFunction_ReturnsDiagnostic()
    {
        const string sql = @"
            CREATE FUNCTION dbo.MyFunc()
            RETURNS INT
            WITH EXECUTE AS OWNER
            AS
            BEGIN
                RETURN 1;
            END;
        ";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("avoid-execute-as", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_ExecuteAsInTrigger_ReturnsDiagnostic()
    {
        const string sql = @"
            CREATE TRIGGER dbo.MyTrigger ON dbo.MyTable
            WITH EXECUTE AS OWNER
            AFTER INSERT
            AS
            BEGIN
                SELECT 1;
            END;
        ";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("avoid-execute-as", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_ExecuteAsWithRevert_StillReturnsDiagnostic()
    {
        const string sql = @"
            EXECUTE AS USER = 'dbo';
            SELECT * FROM sys.databases;
            REVERT;
        ";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("avoid-execute-as", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_MultipleExecuteAs_ReturnsMultipleDiagnostics()
    {
        const string sql = @"
            EXECUTE AS USER = 'dbo';
            REVERT;
            EXECUTE AS LOGIN = 'sa';
            REVERT;
        ";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Equal(2, diagnostics.Length);
        Assert.All(diagnostics, d => Assert.Equal("avoid-execute-as", d.Code));
    }

    [Theory]
    [InlineData("EXEC dbo.MyProc @id = 1;")]
    [InlineData("EXECUTE sp_executesql @stmt;")]
    [InlineData("SELECT * FROM dbo.Users;")]
    [InlineData("")]
    [InlineData("CREATE PROCEDURE dbo.MyProc AS BEGIN SELECT 1; END;")]
    public void Analyze_NoExecuteAs_NoDiagnostic(string sql)
    {
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void GetFixes_ReturnsEmptyCollection()
    {
        const string sql = "EXECUTE AS USER = 'dbo';";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostic = _rule.Analyze(context).First();

        var fixes = _rule.GetFixes(context, diagnostic);

        Assert.Empty(fixes);
    }
}
