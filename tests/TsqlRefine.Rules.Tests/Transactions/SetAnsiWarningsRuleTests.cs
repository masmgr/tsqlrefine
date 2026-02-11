using TsqlRefine.Rules.Rules.Transactions;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Transactions;

public sealed class SetAnsiWarningsRuleTests
{
    [Fact]
    public void Analyze_WhenSetAnsiWarningsOnPresent_ReturnsEmpty()
    {
        var rule = new SetAnsiWarningsRule();
        var sql = "SET ANSI_WARNINGS ON;\nGO\nSELECT 1;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_WhenSetAnsiWarningsMissing_ReturnsDiagnostic()
    {
        var rule = new SetAnsiWarningsRule();
        var sql = "CREATE PROCEDURE dbo.Test AS BEGIN SELECT 1; END;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.NotEmpty(diagnostics);
        Assert.All(diagnostics, d => Assert.Equal("set-ansi-warnings", d.Data?.RuleId));
    }

    [Fact]
    public void Analyze_WhenSetAnsiWarningsOff_ReturnsDiagnostic()
    {
        var rule = new SetAnsiWarningsRule();
        var sql = "SET ANSI_WARNINGS OFF;\nGO\nCREATE PROCEDURE dbo.Test AS BEGIN SELECT 1; END;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.NotEmpty(diagnostics);
    }

    [Fact]
    public void Analyze_WhenSetAnsiWarningsTooLate_ReturnsDiagnostic()
    {
        var rule = new SetAnsiWarningsRule();
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
            "SET ANSI_WARNINGS ON;"
        );
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.NotEmpty(diagnostics);
    }

    [Fact]
    public void Analyze_WhenShortScriptWithoutCreate_ReturnsEmpty()
    {
        var rule = new SetAnsiWarningsRule();
        var context = RuleTestContext.CreateContext("SELECT 1;");

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }
}
