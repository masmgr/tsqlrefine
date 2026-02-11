using TsqlRefine.Rules.Rules.Transactions;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Transactions;

public sealed class SetAnsiPaddingRuleTests
{
    [Fact]
    public void Analyze_WhenSetAnsiPaddingOnPresent_ReturnsEmpty()
    {
        var rule = new SetAnsiPaddingRule();
        var sql = "SET ANSI_PADDING ON;\nGO\nSELECT 1;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_WhenSetAnsiPaddingMissing_ReturnsDiagnostic()
    {
        var rule = new SetAnsiPaddingRule();
        var sql = "CREATE PROCEDURE dbo.Test AS BEGIN SELECT 1; END;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.NotEmpty(diagnostics);
        Assert.All(diagnostics, d => Assert.Equal("set-ansi-padding", d.Data?.RuleId));
    }

    [Fact]
    public void Analyze_WhenSetAnsiPaddingOff_ReturnsDiagnostic()
    {
        var rule = new SetAnsiPaddingRule();
        var sql = "SET ANSI_PADDING OFF;\nGO\nCREATE PROCEDURE dbo.Test AS BEGIN SELECT 1; END;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.NotEmpty(diagnostics);
    }

    [Fact]
    public void Analyze_WhenSetAnsiPaddingTooLate_ReturnsDiagnostic()
    {
        var rule = new SetAnsiPaddingRule();
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
            "SET ANSI_PADDING ON;"
        );
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.NotEmpty(diagnostics);
    }

    [Fact]
    public void Analyze_WhenShortScriptWithoutCreate_ReturnsEmpty()
    {
        var rule = new SetAnsiPaddingRule();
        var context = RuleTestContext.CreateContext("SELECT 1;");

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }
}
