using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Correctness;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Correctness;

public sealed class AvoidNolockRuleTests
{
    [Theory]
    [InlineData("SELECT * FROM users WITH (NOLOCK);")]
    [InlineData("SELECT * FROM users WITH(NOLOCK);")]
    [InlineData("SELECT * FROM users WITH (nolock);")]  // lowercase
    [InlineData("SELECT * FROM users u WITH (NOLOCK) WHERE u.id = 1;")]
    [InlineData("SELECT * FROM dbo.users WITH (NOLOCK);")]
    [InlineData("SELECT u.name FROM users u WITH (NOLOCK);")]
    public void Analyze_WhenNolockTableHint_ReturnsDiagnostic(string sql)
    {
        var rule = new AvoidNolockRule();
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("avoid-nolock", diagnostics[0].Data?.RuleId);
        Assert.Equal("Correctness", diagnostics[0].Data?.Category);
        Assert.False(diagnostics[0].Data?.Fixable);
        Assert.Contains("NOLOCK", diagnostics[0].Message);
    }

    [Theory]
    [InlineData("SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;")]
    [InlineData("SET TRANSACTION ISOLATION LEVEL read uncommitted;")]  // lowercase
    [InlineData("set transaction isolation level read uncommitted;")]  // all lowercase
    public void Analyze_WhenReadUncommittedIsolationLevel_ReturnsDiagnostic(string sql)
    {
        var rule = new AvoidNolockRule();
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("avoid-nolock", diagnostics[0].Data?.RuleId);
        Assert.Equal("Correctness", diagnostics[0].Data?.Category);
        Assert.False(diagnostics[0].Data?.Fixable);
        Assert.Contains("READ UNCOMMITTED", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_MultipleNolockInSameQuery_ReturnsMultipleDiagnostics()
    {
        var rule = new AvoidNolockRule();
        var sql = @"SELECT u.name, o.total
FROM users u WITH (NOLOCK)
INNER JOIN orders o WITH (NOLOCK) ON u.id = o.user_id;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Equal(2, diagnostics.Length);
        Assert.All(diagnostics, d => Assert.Equal("avoid-nolock", d.Data?.RuleId));
        Assert.All(diagnostics, d => Assert.Contains("NOLOCK", d.Message));
    }

    [Fact]
    public void Analyze_MultipleNolockAcrossStatements_ReturnsMultipleDiagnostics()
    {
        var rule = new AvoidNolockRule();
        var sql = @"SELECT * FROM users WITH (NOLOCK);
SELECT * FROM orders WITH (NOLOCK);
SELECT * FROM products WITH (NOLOCK);";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Equal(3, diagnostics.Length);
        Assert.All(diagnostics, d => Assert.Equal("avoid-nolock", d.Data?.RuleId));
    }

    [Fact]
    public void Analyze_NolockWithOtherHints_ReturnsDiagnostic()
    {
        var rule = new AvoidNolockRule();
        var sql = "SELECT * FROM users WITH (NOLOCK, INDEX(idx_name));";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("avoid-nolock", diagnostics[0].Data?.RuleId);
    }

    [Fact]
    public void Analyze_ReadUncommittedWithOtherStatements_ReturnsDiagnostic()
    {
        var rule = new AvoidNolockRule();
        var sql = @"SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;
SELECT * FROM users;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("avoid-nolock", diagnostics[0].Data?.RuleId);
    }

    [Theory]
    [InlineData("SELECT * FROM users;")]
    [InlineData("SELECT * FROM users WITH (ROWLOCK);")]
    [InlineData("SELECT * FROM users WITH (UPDLOCK);")]
    [InlineData("SELECT * FROM users WITH (READPAST);")]
    [InlineData("SELECT * FROM users WITH (INDEX(idx_name));")]
    [InlineData("SET TRANSACTION ISOLATION LEVEL READ COMMITTED;")]
    [InlineData("SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;")]
    [InlineData("SET TRANSACTION ISOLATION LEVEL SNAPSHOT;")]
    [InlineData("SET TRANSACTION ISOLATION LEVEL REPEATABLE READ;")]
    [InlineData("SELECT * FROM users WHERE name LIKE '%nolock%';")]  // NOLOCK in string, not hint
    [InlineData("UPDATE users SET status = 'active';")]
    [InlineData("INSERT INTO users (name) VALUES ('test');")]
    [InlineData("DELETE FROM users WHERE id = 1;")]
    public void Analyze_WhenNotViolating_ReturnsEmpty(string sql)
    {
        var rule = new AvoidNolockRule();
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_NolockInSubquery_ReturnsDiagnostic()
    {
        var rule = new AvoidNolockRule();
        var sql = @"SELECT u.name
FROM users u
WHERE u.id IN (SELECT user_id FROM orders WITH (NOLOCK));";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("avoid-nolock", diagnostics[0].Data?.RuleId);
    }

    [Fact]
    public void Analyze_NolockInCte_ReturnsDiagnostic()
    {
        var rule = new AvoidNolockRule();
        var sql = @"WITH UserOrders AS (
    SELECT user_id, COUNT(*) as order_count
    FROM orders WITH (NOLOCK)
    GROUP BY user_id
)
SELECT * FROM UserOrders;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("avoid-nolock", diagnostics[0].Data?.RuleId);
    }

    [Fact]
    public void Analyze_EmptyInput_ReturnsEmpty()
    {
        var rule = new AvoidNolockRule();
        var context = RuleTestContext.CreateContext("");

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void GetFixes_ReturnsEmpty()
    {
        var rule = new AvoidNolockRule();
        var context = RuleTestContext.CreateContext("SELECT * FROM users WITH (NOLOCK);");
        var diagnostic = new Diagnostic(
            Range: new TsqlRefine.PluginSdk.Range(new Position(0, 0), new Position(0, 6)),
            Message: "test",
            Code: "avoid-nolock"
        );

        var fixes = rule.GetFixes(context, diagnostic).ToArray();

        Assert.Empty(fixes);
    }

    [Fact]
    public void Metadata_HasCorrectValues()
    {
        var rule = new AvoidNolockRule();

        Assert.Equal("avoid-nolock", rule.Metadata.RuleId);
        Assert.Equal("Correctness", rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Warning, rule.Metadata.DefaultSeverity);
        Assert.False(rule.Metadata.Fixable);
        Assert.Contains("NOLOCK", rule.Metadata.Description);
    }

    [Fact]
    public void Analyze_NolockTableHint_ReportsAtTableHint()
    {
        var rule = new AvoidNolockRule();
        var sql = "SELECT * FROM users WITH (NOLOCK);";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        var diagnostic = diagnostics[0];

        // The table hint should be somewhere after "WITH"
        Assert.Equal(0, diagnostic.Range.Start.Line);
        Assert.True(diagnostic.Range.Start.Character >= 20); // After "users WITH "
    }

    [Fact]
    public void Analyze_ReadUncommitted_ReportsAtStatement()
    {
        var rule = new AvoidNolockRule();
        var sql = "SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        var diagnostic = diagnostics[0];

        // The statement should start at the beginning
        Assert.Equal(0, diagnostic.Range.Start.Line);
        Assert.Equal(0, diagnostic.Range.Start.Character);
    }

    [Fact]
    public void Analyze_BothNolockAndReadUncommitted_ReturnsBothDiagnostics()
    {
        var rule = new AvoidNolockRule();
        var sql = @"SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;
SELECT * FROM users WITH (NOLOCK);";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Equal(2, diagnostics.Length);
        Assert.All(diagnostics, d => Assert.Equal("avoid-nolock", d.Data?.RuleId));

        // One should mention READ UNCOMMITTED, one should mention NOLOCK
        Assert.Contains(diagnostics, d => d.Message.Contains("READ UNCOMMITTED"));
        Assert.Contains(diagnostics, d => d.Message.Contains("NOLOCK"));
    }


}
