using TsqlRefine.Rules.Rules.Transactions;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Transactions;

public sealed class SetTransactionIsolationLevelRuleTests
{
    [Fact]
    public void Analyze_WhenSetTransactionIsolationLevelPresentBeforeCreate_ReturnsEmpty()
    {
        var rule = new SetTransactionIsolationLevelRule();
        var sql = "SET TRANSACTION ISOLATION LEVEL READ COMMITTED;\nGO\nCREATE PROCEDURE dbo.Test AS BEGIN SELECT 1; END;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_WhenSetTransactionIsolationLevelMissingWithCreate_ReturnsDiagnostic()
    {
        var rule = new SetTransactionIsolationLevelRule();
        var sql = "CREATE PROCEDURE dbo.Test AS BEGIN SELECT 1; END;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.NotEmpty(diagnostics);
        Assert.All(diagnostics, d => Assert.Equal("set-transaction-isolation-level", d.Data?.RuleId));
    }

    [Fact]
    public void Analyze_WhenSetTransactionIsolationLevelTooLate_ReturnsDiagnostic()
    {
        var rule = new SetTransactionIsolationLevelRule();
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
            "SET TRANSACTION ISOLATION LEVEL READ COMMITTED;"
        );
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.NotEmpty(diagnostics);
    }

    [Fact]
    public void Analyze_WhenShortScriptWithoutCreate_ReturnsEmpty()
    {
        var rule = new SetTransactionIsolationLevelRule();
        var context = RuleTestContext.CreateContext("SELECT 1;");

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }
}
