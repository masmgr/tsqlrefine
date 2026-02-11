using TsqlRefine.Rules.Rules.Transactions;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Transactions;

public sealed class SetArithabortRuleTests
{
    [Fact]
    public void Analyze_WhenSetArithabortOnPresent_ReturnsEmpty()
    {
        var rule = new SetArithabortRule();
        var sql = "SET ARITHABORT ON;\nGO\nSELECT 1;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_WhenSetArithabortMissing_ReturnsDiagnostic()
    {
        var rule = new SetArithabortRule();
        var sql = "CREATE PROCEDURE dbo.Test AS BEGIN SELECT 1; END;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.NotEmpty(diagnostics);
        Assert.All(diagnostics, d => Assert.Equal("set-arithabort", d.Data?.RuleId));
    }

    [Fact]
    public void Analyze_WhenSetArithabortOff_ReturnsDiagnostic()
    {
        var rule = new SetArithabortRule();
        var sql = "SET ARITHABORT OFF;\nGO\nCREATE PROCEDURE dbo.Test AS BEGIN SELECT 1; END;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.NotEmpty(diagnostics);
    }

    [Fact]
    public void Analyze_WhenSetArithabortTooLate_ReturnsDiagnostic()
    {
        var rule = new SetArithabortRule();
        var sql = string.Join("\n",
            "SELECT 1;",
            "SELECT 2;",
            "SELECT 3;",
            "SELECT 4;",
            "SELECT 5;",
            "SELECT 6;",
            "SELECT 7;",
            "SELECT 8;",
            "SELECT 9;",
            "SELECT 10;",
            "SELECT 11;",
            "SET ARITHABORT ON;"
        );
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.NotEmpty(diagnostics);
    }

    [Fact]
    public void Analyze_WhenShortScriptWithoutCreate_ReturnsEmpty()
    {
        var rule = new SetArithabortRule();
        var context = RuleTestContext.CreateContext("SELECT 1;");

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }
}
