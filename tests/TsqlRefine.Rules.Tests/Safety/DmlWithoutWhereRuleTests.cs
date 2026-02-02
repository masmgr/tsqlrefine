using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Safety;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Safety;

public sealed class DmlWithoutWhereRuleTests
{
    [Theory]
    [InlineData("UPDATE users SET active = 1;")]
    [InlineData("DELETE FROM orders;")]
    [InlineData("UPDATE dbo.products SET price = price * 1.1;")]
    [InlineData("DELETE orders;")]
    [InlineData("update users set active = 1;")]  // lowercase
    [InlineData("delete from orders;")]  // lowercase
    [InlineData("UPDATE users SET active = 1 FROM users LEFT JOIN orders ON users.id = orders.user_id;")]  // LEFT JOIN does NOT exempt
    [InlineData("UPDATE users SET active = 1 FROM users CROSS JOIN orders;")]  // CROSS JOIN does NOT exempt
    public void Analyze_WhenDmlWithoutWhere_ReturnsDiagnostic(string sql)
    {
        var rule = new DmlWithoutWhereRule();
        var context = CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("dml-without-where", diagnostics[0].Data?.RuleId);
        Assert.Equal("Safety", diagnostics[0].Data?.Category);
        Assert.False(diagnostics[0].Data?.Fixable);
        Assert.Contains("WHERE", diagnostics[0].Message);
    }

    [Theory]
    [InlineData("UPDATE users SET active = 1 WHERE id = 5;")]
    [InlineData("DELETE FROM orders WHERE status = 'cancelled';")]
    [InlineData("UPDATE users SET active = 1 WHERE created_at < '2020-01-01';")]
    [InlineData("DELETE FROM orders WHERE order_date < DATEADD(year, -1, GETDATE());")]
    [InlineData("SELECT * FROM users;")]  // not DML
    [InlineData("INSERT INTO users (name) VALUES ('test');")]  // INSERT is allowed
    [InlineData("UPDATE #temp SET col = 1;")]  // local temp table
    [InlineData("DELETE FROM #temp;")]  // local temp table
    [InlineData("UPDATE ##globaltemp SET col = 1;")]  // global temp table
    [InlineData("DELETE FROM ##globaltemp;")]  // global temp table
    [InlineData("UPDATE @tablevar SET col = 1;")]  // table variable
    [InlineData("DELETE FROM @tablevar;")]  // table variable
    [InlineData("UPDATE u SET active = 1 FROM users u;")]  // alias target
    [InlineData("DELETE u FROM users u;")]  // alias target
    [InlineData("UPDATE u SET active = 1 FROM dbo.users u;")]  // alias target with schema
    [InlineData("DELETE u FROM dbo.users AS u;")]  // alias target with AS keyword
    [InlineData("UPDATE users SET active = 1 FROM users INNER JOIN orders ON users.id = orders.user_id;")]  // INNER JOIN
    [InlineData("DELETE FROM users FROM users INNER JOIN orders ON users.id = orders.user_id;")]  // INNER JOIN
    [InlineData("UPDATE u SET active = 1 FROM users u INNER JOIN orders o ON u.id = o.user_id;")]  // alias + INNER JOIN
    [InlineData("DELETE u FROM users u INNER JOIN orders o ON u.id = o.user_id;")]  // alias + INNER JOIN
    public void Analyze_WhenNotViolating_ReturnsEmpty(string sql)
    {
        var rule = new DmlWithoutWhereRule();
        var context = CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_UpdateWithoutWhere_ReportsAtUpdateKeyword()
    {
        var rule = new DmlWithoutWhereRule();
        var sql = "UPDATE users SET active = 1;";
        var context = CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        var diagnostic = diagnostics[0];

        // UPDATE keyword starts at position 0,0 and ends at 0,6 ("UPDATE" is 6 characters)
        Assert.Equal(0, diagnostic.Range.Start.Line);
        Assert.Equal(0, diagnostic.Range.Start.Character);
    }

    [Fact]
    public void Analyze_DeleteWithoutWhere_ReportsAtDeleteKeyword()
    {
        var rule = new DmlWithoutWhereRule();
        var sql = "DELETE FROM orders;";
        var context = CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        var diagnostic = diagnostics[0];

        // DELETE keyword starts at position 0,0
        Assert.Equal(0, diagnostic.Range.Start.Line);
        Assert.Equal(0, diagnostic.Range.Start.Character);
    }

    [Fact]
    public void Analyze_MultipleViolations_ReturnsMultipleDiagnostics()
    {
        var rule = new DmlWithoutWhereRule();
        var sql = @"UPDATE users SET active = 1;
DELETE FROM orders;";
        var context = CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Equal(2, diagnostics.Length);
        Assert.All(diagnostics, d => Assert.Equal("dml-without-where", d.Data?.RuleId));
    }

    [Fact]
    public void Analyze_EmptyInput_ReturnsEmpty()
    {
        var rule = new DmlWithoutWhereRule();
        var context = CreateContext("");

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void GetFixes_ReturnsEmpty()
    {
        var rule = new DmlWithoutWhereRule();
        var context = CreateContext("UPDATE users SET active = 1;");
        var diagnostic = new Diagnostic(
            Range: new TsqlRefine.PluginSdk.Range(new Position(0, 0), new Position(0, 6)),
            Message: "test",
            Code: "dml-without-where"
        );

        var fixes = rule.GetFixes(context, diagnostic).ToArray();

        Assert.Empty(fixes);
    }

    [Fact]
    public void Metadata_HasCorrectValues()
    {
        var rule = new DmlWithoutWhereRule();

        Assert.Equal("dml-without-where", rule.Metadata.RuleId);
        Assert.Equal("Safety", rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Error, rule.Metadata.DefaultSeverity);
        Assert.False(rule.Metadata.Fixable);
        Assert.Contains("UPDATE", rule.Metadata.Description);
        Assert.Contains("DELETE", rule.Metadata.Description);
    }

    [Fact]
    public void Analyze_UpdateWithAliasTarget_ReturnsEmpty()
    {
        var rule = new DmlWithoutWhereRule();
        var sql = "UPDATE u SET active = 1 FROM users u;";
        var context = CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_DeleteWithAliasTarget_ReturnsEmpty()
    {
        var rule = new DmlWithoutWhereRule();
        var sql = "DELETE u FROM users u;";
        var context = CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_UpdateWithInnerJoin_ReturnsEmpty()
    {
        var rule = new DmlWithoutWhereRule();
        var sql = "UPDATE users SET active = 1 FROM users INNER JOIN orders ON users.id = orders.user_id;";
        var context = CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_DeleteWithInnerJoin_ReturnsEmpty()
    {
        var rule = new DmlWithoutWhereRule();
        var sql = "DELETE FROM users FROM users INNER JOIN orders ON users.id = orders.user_id;";
        var context = CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_UpdateWithLeftJoin_ReturnsDiagnostic()
    {
        var rule = new DmlWithoutWhereRule();
        var sql = "UPDATE users SET active = 1 FROM users LEFT JOIN orders ON users.id = orders.user_id;";
        var context = CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("dml-without-where", diagnostics[0].Data?.RuleId);
    }

    [Fact]
    public void Analyze_UpdateWithNestedInnerJoin_ReturnsEmpty()
    {
        var rule = new DmlWithoutWhereRule();
        var sql = @"UPDATE u
            SET active = 1
            FROM users u
            LEFT JOIN orders o ON u.id = o.user_id
            INNER JOIN customers c ON o.customer_id = c.id;";
        var context = CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    private static RuleContext CreateContext(string sql)
    {
        return RuleTestContext.CreateContext(sql);
    }
}
