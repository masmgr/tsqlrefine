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

    [Fact]
    public void Analyze_WhenQualifiedWildcard_ReturnsEmpty()
    {
        var rule = new AvoidSelectStarRule();
        var sql = "SELECT u.* FROM users u;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_WhenMultiPartQualifiedWildcard_ReturnsEmpty()
    {
        var rule = new AvoidSelectStarRule();
        var sql = "SELECT dbo.users.* FROM dbo.users;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_WhenSelectStarInSubquery_ReturnsDiagnostic()
    {
        var rule = new AvoidSelectStarRule();
        var sql = "SELECT u.UserName FROM (SELECT * FROM dbo.Users) u;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
    }

    [Fact]
    public void Analyze_WhenNestedExists_ReturnsEmpty()
    {
        var rule = new AvoidSelectStarRule();
        var sql = "SELECT id FROM t1 WHERE EXISTS (SELECT * FROM t2 WHERE EXISTS (SELECT * FROM t3));";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_WhenSelectTopStar_ReturnsDiagnostic()
    {
        var rule = new AvoidSelectStarRule();
        var sql = "SELECT TOP 1 * FROM users;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
    }

    [Fact]
    public void Analyze_WhenSelectDistinctStar_ReturnsDiagnostic()
    {
        var rule = new AvoidSelectStarRule();
        var sql = "SELECT DISTINCT * FROM users;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
    }

    [Fact]
    public void Analyze_WhenCountStar_ReturnsEmpty()
    {
        var rule = new AvoidSelectStarRule();
        var sql = "SELECT COUNT(*) FROM users;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_WhenMultipleSelectStar_ReportsAll()
    {
        var rule = new AvoidSelectStarRule();
        var sql = "SELECT * FROM t1; SELECT * FROM t2;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Equal(2, diagnostics.Length);
    }
}
