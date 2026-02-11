using TsqlRefine.Rules.Rules.Transactions;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Transactions;

public sealed class SetQuotedIdentifierRuleTests
{
    [Fact]
    public void Analyze_WhenSetQuotedIdentifierOnPresentBeforeCreate_ReturnsEmpty()
    {
        var rule = new SetQuotedIdentifierRule();
        var sql = "SET QUOTED_IDENTIFIER ON;\nGO\nCREATE PROCEDURE dbo.Test AS BEGIN SELECT 1; END;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_WhenSetQuotedIdentifierMissingWithCreate_ReturnsDiagnostic()
    {
        var rule = new SetQuotedIdentifierRule();
        var sql = "CREATE PROCEDURE dbo.Test AS BEGIN SELECT 1; END;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.NotEmpty(diagnostics);
        Assert.All(diagnostics, d => Assert.Equal("set-quoted-identifier", d.Data?.RuleId));
    }

    [Fact]
    public void Analyze_WhenSetQuotedIdentifierOff_ReturnsDiagnostic()
    {
        var rule = new SetQuotedIdentifierRule();
        var sql = "SET QUOTED_IDENTIFIER OFF;\nGO\nCREATE PROCEDURE dbo.Test AS BEGIN SELECT 1; END;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.NotEmpty(diagnostics);
    }

    [Fact]
    public void Analyze_WhenSetQuotedIdentifierTooLate_ReturnsDiagnostic()
    {
        var rule = new SetQuotedIdentifierRule();
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
            "SET QUOTED_IDENTIFIER ON;"
        );
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.NotEmpty(diagnostics);
    }

    [Fact]
    public void Analyze_WhenShortScriptWithoutCreate_ReturnsEmpty()
    {
        var rule = new SetQuotedIdentifierRule();
        var context = RuleTestContext.CreateContext("SELECT 1;");

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }
}
