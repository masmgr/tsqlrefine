using TsqlRefine.Rules.Rules.Transactions;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Transactions;

public sealed class SetAnsiRuleTests
{
    [Fact]
    public void Analyze_WhenSetAnsiNullsOnPresent_ReturnsEmpty()
    {
        var rule = new SetAnsiRule();
        var sql = "SET ANSI_NULLS ON;\nGO\nSELECT 1;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_WhenSetAnsiNullsMissing_ReturnsDiagnostic()
    {
        var rule = new SetAnsiRule();
        var sql = "CREATE PROCEDURE dbo.Test AS BEGIN SELECT 1; END;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.NotEmpty(diagnostics);
        Assert.All(diagnostics, d => Assert.Equal("set-ansi", d.Data?.RuleId));
    }

    [Fact]
    public void Analyze_WhenSetAnsiNullsOff_ReturnsDiagnostic()
    {
        var rule = new SetAnsiRule();
        var sql = "SET ANSI_NULLS OFF;\nGO\nCREATE PROCEDURE dbo.Test AS BEGIN SELECT 1; END;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.NotEmpty(diagnostics);
    }

    [Fact]
    public void Analyze_WhenSetAnsiNullsTooLate_ReturnsDiagnostic()
    {
        var rule = new SetAnsiRule();
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
            "SET ANSI_NULLS ON;"
        );
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.NotEmpty(diagnostics);
    }

}
