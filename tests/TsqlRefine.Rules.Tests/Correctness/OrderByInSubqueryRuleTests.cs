using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Correctness;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Correctness;

public sealed class OrderByInSubqueryRuleTests
{
    private readonly OrderByInSubqueryRule _rule = new();

    [Theory]
    [InlineData(@"SELECT * FROM (SELECT id, name FROM users ORDER BY name) AS subquery;")]
    [InlineData(@"SELECT * FROM users WHERE id IN (SELECT user_id FROM orders ORDER BY total);")]
    [InlineData(@"
        SELECT * FROM (
            SELECT * FROM (
                SELECT id FROM users ORDER BY id
            ) AS inner_sub ORDER BY id
        ) AS outer_sub;")]
    [InlineData(@"
        WITH UsersCte AS (
            SELECT id, name
            FROM users
            ORDER BY name
        )
        SELECT * FROM UsersCte;")]
    public void Analyze_OrderByInSubqueryWithoutAllowedException_ReturnsDiagnostic(string sql)
    {
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.NotEmpty(diagnostics);
        Assert.All(diagnostics, d => Assert.Equal("order-by-in-subquery", d.Code));
    }

    [Theory]
    [InlineData(@"SELECT * FROM (SELECT TOP 10 id, name FROM users ORDER BY name) AS subquery;")]
    [InlineData(@"SELECT * FROM (SELECT id FROM users ORDER BY name OFFSET 0 ROWS FETCH NEXT 10 ROWS ONLY) AS sub;")]
    [InlineData(@"SELECT * FROM (SELECT id, name FROM users ORDER BY name FOR XML PATH('')) AS sub;")]
    [InlineData(@"SELECT * FROM (SELECT id, name FROM users ORDER BY name FOR JSON PATH) AS sub;")]
    [InlineData(@"SELECT id, name FROM users ORDER BY name;")]
    [InlineData(@"SELECT * FROM users;")]
    [InlineData(@"")]
    public void Analyze_WhenValid_ReturnsEmpty(string sql)
    {
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_CteOrderByWithTop_ReturnsEmpty()
    {
        const string sql = @"
            WITH UsersCte AS (
                SELECT TOP 10 id, name
                FROM users
                ORDER BY name
            )
            SELECT * FROM UsersCte;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_DerivedTableWithOrderByForBrowse_ReturnsDiagnostic()
    {
        const string sql = @"
            SELECT *
            FROM (
                SELECT id
                FROM users
                ORDER BY id FOR BROWSE
            ) AS d;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("order-by-in-subquery", diagnostics[0].Code);
    }

    [Fact]
    public void Metadata_HasCorrectProperties()
    {
        Assert.Equal("order-by-in-subquery", _rule.Metadata.RuleId);
        Assert.Equal("Correctness", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Warning, _rule.Metadata.DefaultSeverity);
        Assert.False(_rule.Metadata.Fixable);
    }

    [Fact]
    public void GetFixes_ReturnsEmpty()
    {
        var context = RuleTestContext.CreateContext("SELECT * FROM (SELECT id FROM users ORDER BY id) AS sub;");
        var diagnostic = new Diagnostic(
            Range: new TsqlRefine.PluginSdk.Range(new Position(0, 0), new Position(0, 10)),
            Message: "test",
            Code: "order-by-in-subquery"
        );

        var fixes = _rule.GetFixes(context, diagnostic).ToArray();

        Assert.Empty(fixes);
    }
}
