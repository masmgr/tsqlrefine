using TsqlRefine.Rules.Rules;
using TsqlRefine.Rules.Rules.Performance;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Performance;

public sealed class AvoidSelectStarRuleTests
{
    [Fact]
    public void Analyze_WhenSelectStar_ReturnsDiagnostic()
    {
        var rule = new AvoidSelectStarRule();
        var sql = "select * from t;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("avoid-select-star", diagnostics[0].Data?.RuleId);
        Assert.Equal(0, diagnostics[0].Range.Start.Line);
        Assert.Equal(7, diagnostics[0].Range.Start.Character);
        Assert.Equal(0, diagnostics[0].Range.End.Line);
        Assert.Equal(8, diagnostics[0].Range.End.Character);
    }

    [Fact]
    public void Analyze_WhenNoSelectStar_ReturnsEmpty()
    {
        var rule = new AvoidSelectStarRule();
        var sql = "select id from t;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_WhenSelectStarInExists_ReturnsEmpty()
    {
        var rule = new AvoidSelectStarRule();
        var sql = "select id from t1 where exists (select * from t2 where t2.id = t1.id);";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_WhenSelectStarInNotExists_ReturnsEmpty()
    {
        var rule = new AvoidSelectStarRule();
        var sql = "select id from t1 where not exists (select * from t2 where t2.id = t1.id);";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_WhenSelectStarOutsideExists_ReturnsDiagnostic()
    {
        var rule = new AvoidSelectStarRule();
        var sql = "select * from t1 where exists (select 1 from t2);";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("avoid-select-star", diagnostics[0].Data?.RuleId);
    }

}
