using TsqlRefine.Rules.Rules.Transactions;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Transactions;

public sealed class SetConcatNullYieldsNullRuleTests
{
    [Fact]
    public void Analyze_WhenSetConcatNullYieldsNullOnPresent_ReturnsEmpty()
    {
        var rule = new SetConcatNullYieldsNullRule();
        var sql = "SET CONCAT_NULL_YIELDS_NULL ON;\nGO\nSELECT 1;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_WhenSetConcatNullYieldsNullMissing_ReturnsDiagnostic()
    {
        var rule = new SetConcatNullYieldsNullRule();
        var sql = "CREATE PROCEDURE dbo.Test AS BEGIN SELECT 1; END;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.NotEmpty(diagnostics);
        Assert.All(diagnostics, d => Assert.Equal("set-concat-null-yields-null", d.Data?.RuleId));
    }

    [Fact]
    public void Analyze_WhenSetConcatNullYieldsNullOff_ReturnsDiagnostic()
    {
        var rule = new SetConcatNullYieldsNullRule();
        var sql = "SET CONCAT_NULL_YIELDS_NULL OFF;\nGO\nCREATE PROCEDURE dbo.Test AS BEGIN SELECT 1; END;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.NotEmpty(diagnostics);
    }

    [Fact]
    public void Analyze_WhenSetConcatNullYieldsNullTooLate_ReturnsDiagnostic()
    {
        var rule = new SetConcatNullYieldsNullRule();
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
            "SET CONCAT_NULL_YIELDS_NULL ON;"
        );
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.NotEmpty(diagnostics);
    }

    [Fact]
    public void Analyze_WhenShortScriptWithoutCreate_ReturnsEmpty()
    {
        var rule = new SetConcatNullYieldsNullRule();
        var context = RuleTestContext.CreateContext("SELECT 1;");

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }
}
