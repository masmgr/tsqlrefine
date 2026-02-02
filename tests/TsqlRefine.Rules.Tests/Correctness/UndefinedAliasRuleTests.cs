using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Correctness.Semantic;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Correctness;

public sealed class UndefinedAliasRuleTests
{
    [Theory]
    [InlineData("SELECT u.id FROM users WHERE x.active = 1;")]
    [InlineData("SELECT t.name FROM users;")]
    [InlineData("SELECT u.id FROM users WHERE u.id = v.id;")]
    [InlineData("SELECT a.id, b.name FROM users WHERE c.active = 1;")]
    public void Analyze_WhenUndefinedAlias_ReturnsDiagnostic(string sql)
    {
        var rule = new UndefinedAliasRule();
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.NotEmpty(diagnostics);
        Assert.Contains(diagnostics, d => d.Data?.RuleId == "semantic/undefined-alias");
        Assert.All(diagnostics.Where(d => d.Data?.RuleId == "semantic/undefined-alias"), d =>
        {
            Assert.Equal("Correctness", d.Data?.Category);
            Assert.False(d.Data?.Fixable);
            Assert.Contains("undefined", d.Message, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Theory]
    [InlineData("SELECT u.id FROM users u WHERE u.active = 1;")]
    [InlineData("SELECT users.id FROM users;")]  // implicit table name
    [InlineData("SELECT * FROM users;")]  // no qualified references
    [InlineData("SELECT u.id, o.order_id FROM users u JOIN orders o ON u.id = o.user_id;")]
    [InlineData("SELECT u.id FROM users u;")]
    [InlineData("SELECT id FROM users;")]  // unqualified column
    public void Analyze_WhenNotViolating_ReturnsEmpty(string sql)
    {
        var rule = new UndefinedAliasRule();
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).Where(d => d.Data?.RuleId == "semantic/undefined-alias").ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_UndefinedAlias_ReportsAtColumnReference()
    {
        var rule = new UndefinedAliasRule();
        var sql = "SELECT x.id FROM users u;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).Where(d => d.Data?.RuleId == "semantic/undefined-alias").ToArray();

        Assert.Single(diagnostics);
        var diagnostic = diagnostics[0];
        Assert.Contains("x", diagnostic.Message);
    }

    [Fact]
    public void Analyze_MultipleUndefinedAliases_ReturnsMultipleDiagnostics()
    {
        var rule = new UndefinedAliasRule();
        var sql = "SELECT a.id, b.name FROM users u WHERE c.active = 1;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).Where(d => d.Data?.RuleId == "semantic/undefined-alias").ToArray();

        Assert.Equal(3, diagnostics.Length);
        Assert.Contains(diagnostics, d => d.Message.Contains("a"));
        Assert.Contains(diagnostics, d => d.Message.Contains("b"));
        Assert.Contains(diagnostics, d => d.Message.Contains("c"));
    }

    [Fact]
    public void Analyze_CaseInsensitive_RecognizesAlias()
    {
        var rule = new UndefinedAliasRule();
        var sql = "SELECT MyAlias.id FROM users myalias;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).Where(d => d.Data?.RuleId == "semantic/undefined-alias").ToArray();

        // Should not report error because MyAlias matches myalias (case-insensitive)
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_WithTableSchema_RecognizesImplicitName()
    {
        var rule = new UndefinedAliasRule();
        var sql = "SELECT users.id FROM dbo.users;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).Where(d => d.Data?.RuleId == "semantic/undefined-alias").ToArray();

        // Should recognize 'users' as the implicit table name
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_WithExplicitAlias_IgnoresTableName()
    {
        var rule = new UndefinedAliasRule();
        var sql = "SELECT users.id FROM users u;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).Where(d => d.Data?.RuleId == "semantic/undefined-alias").ToArray();

        // When alias 'u' is provided, 'users' is not a valid qualifier
        Assert.Single(diagnostics);
        Assert.Contains("users", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_MultipleQueriesInBatch_ValidatesEachIndependently()
    {
        var rule = new UndefinedAliasRule();
        var sql = @"SELECT u.id FROM users u;
SELECT x.id FROM orders o;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).Where(d => d.Data?.RuleId == "semantic/undefined-alias").ToArray();

        // Only the second query should have an error (x is undefined)
        Assert.Single(diagnostics);
        Assert.Contains("x", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_SubqueryReferences_ValidatedIndependently()
    {
        var rule = new UndefinedAliasRule();
        // Outer query references 'u', which is defined in outer scope
        // Inner subquery references 'u', which is defined in inner scope
        // This is valid (each SELECT has its own scope)
        var sql = "SELECT u.id FROM (SELECT id FROM users u) AS u;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).Where(d => d.Data?.RuleId == "semantic/undefined-alias").ToArray();

        // Inner 'u' is a table alias in subquery
        // Outer 'u' is the subquery alias (derived table)
        // Both are valid in their respective scopes
        // With simple MVP approach, we validate each SELECT independently
        // Outer SELECT sees subquery alias 'u' - this is NOT a column reference, so no diagnostic
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_JoinConditionReferences_Validated()
    {
        var rule = new UndefinedAliasRule();
        var sql = "SELECT * FROM users u JOIN orders o ON x.id = o.user_id;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).Where(d => d.Data?.RuleId == "semantic/undefined-alias").ToArray();

        Assert.Single(diagnostics);
        Assert.Contains("x", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_OrderByClauseReferences_Validated()
    {
        var rule = new UndefinedAliasRule();
        var sql = "SELECT u.id FROM users u ORDER BY x.created_at;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).Where(d => d.Data?.RuleId == "semantic/undefined-alias").ToArray();

        Assert.Single(diagnostics);
        Assert.Contains("x", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_EmptyInput_ReturnsEmpty()
    {
        var rule = new UndefinedAliasRule();
        var context = RuleTestContext.CreateContext("");

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void GetFixes_ReturnsEmpty()
    {
        var rule = new UndefinedAliasRule();
        var context = RuleTestContext.CreateContext("SELECT x.id FROM users u;");
        var diagnostic = new Diagnostic(
            Range: new TsqlRefine.PluginSdk.Range(new Position(0, 7), new Position(0, 11)),
            Message: "test",
            Code: "semantic/undefined-alias"
        );

        var fixes = rule.GetFixes(context, diagnostic).ToArray();

        Assert.Empty(fixes);
    }

    [Fact]
    public void Metadata_HasCorrectValues()
    {
        var rule = new UndefinedAliasRule();

        Assert.Equal("semantic/undefined-alias", rule.Metadata.RuleId);
        Assert.Equal("Correctness", rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Error, rule.Metadata.DefaultSeverity);
        Assert.False(rule.Metadata.Fixable);
        Assert.Contains("undefined", rule.Metadata.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("alias", rule.Metadata.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("SELECT dbo.users.id FROM dbo.users;")]   // schema.table.column
    [InlineData("SELECT dbo.users.id FROM users;")]        // schema prefix on column
    [InlineData("SELECT mydb.dbo.users.id FROM users;")]   // server.schema.table.column
    public void Analyze_WithSchemaQualifiedColumn_RecognizesTableName(string sql)
    {
        var rule = new UndefinedAliasRule();
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context)
            .Where(d => d.Data?.RuleId == "semantic/undefined-alias")
            .ToArray();

        Assert.Empty(diagnostics);
    }

    [Theory]
    [InlineData("SELECT dbo.unknown.id FROM users;", "unknown")]        // unknown table with schema
    [InlineData("SELECT mydb.dbo.unknown.id FROM users;", "unknown")]   // unknown table with server.schema
    public void Analyze_WithSchemaQualifiedUnknownTable_ReturnsDiagnostic(string sql, string expectedAlias)
    {
        var rule = new UndefinedAliasRule();
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context)
            .Where(d => d.Data?.RuleId == "semantic/undefined-alias")
            .ToArray();

        Assert.Single(diagnostics);
        Assert.Contains(expectedAlias, diagnostics[0].Message);
    }

    [Theory]
    [InlineData("SELECT tvf.col FROM dbo.f_func() AS tvf;")]
    [InlineData("SELECT tvf.col FROM dbo.f_func() tvf;")]  // without AS keyword
    [InlineData("SELECT a.id, tvf.col FROM users a JOIN dbo.f_func() AS tvf ON a.id = tvf.user_id;")]
    [InlineData("SELECT t.col FROM schema1.f_tableFunc(@param) AS t;")]  // with parameter
    public void Analyze_WithTableValuedFunction_RecognizesAlias(string sql)
    {
        var rule = new UndefinedAliasRule();
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context)
            .Where(d => d.Data?.RuleId == "semantic/undefined-alias")
            .ToArray();

        Assert.Empty(diagnostics);
    }

    [Theory]
    [InlineData("SELECT x.col FROM dbo.f_func() AS tvf;", "x")]
    [InlineData("SELECT tvf.col, y.other FROM dbo.f_func() AS tvf;", "y")]
    public void Analyze_WithTableValuedFunctionAndUndefinedAlias_ReturnsDiagnostic(string sql, string expectedAlias)
    {
        var rule = new UndefinedAliasRule();
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context)
            .Where(d => d.Data?.RuleId == "semantic/undefined-alias")
            .ToArray();

        Assert.Single(diagnostics);
        Assert.Contains(expectedAlias, diagnostics[0].Message);
    }

    #region Subquery Scope Tests

    // FROM clause subquery (QueryDerivedTable) - Valid cases
    [Theory]
    [InlineData("SELECT sub.col FROM (SELECT t.id AS col FROM table1 t) AS sub;")]
    [InlineData("SELECT a.x, b.y FROM (SELECT id AS x FROM t1) a JOIN (SELECT id AS y FROM t2) b ON a.x = b.y;")]
    [InlineData("SELECT u.id FROM (SELECT id FROM users) AS u;")]
    public void Analyze_FromClauseSubquery_ValidAliases_ReturnsEmpty(string sql)
    {
        var rule = new UndefinedAliasRule();
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context)
            .Where(d => d.Data?.RuleId == "semantic/undefined-alias")
            .ToArray();

        Assert.Empty(diagnostics);
    }

    // FROM clause subquery - Invalid cases
    [Theory]
    [InlineData("SELECT x.id FROM (SELECT id FROM users) AS u;", "x")]
    [InlineData("SELECT sub.col FROM (SELECT x.id FROM table1 t) AS sub;", "x")]
    public void Analyze_FromClauseSubquery_UndefinedAlias_ReturnsDiagnostic(string sql, string expectedAlias)
    {
        var rule = new UndefinedAliasRule();
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context)
            .Where(d => d.Data?.RuleId == "semantic/undefined-alias")
            .ToArray();

        Assert.Single(diagnostics);
        Assert.Contains(expectedAlias, diagnostics[0].Message);
    }

    // SELECT clause scalar subquery - Valid cases (including correlated)
    [Theory]
    [InlineData("SELECT (SELECT t.id FROM table1 t) FROM users u;")]
    [InlineData("SELECT u.name, (SELECT COUNT(*) FROM orders o WHERE o.user_id = u.id) FROM users u;")]  // correlated
    [InlineData("SELECT (SELECT o.total FROM orders o WHERE o.user_id = u.id) AS order_total FROM users u;")]  // correlated
    public void Analyze_SelectClauseSubquery_ValidAliases_ReturnsEmpty(string sql)
    {
        var rule = new UndefinedAliasRule();
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context)
            .Where(d => d.Data?.RuleId == "semantic/undefined-alias")
            .ToArray();

        Assert.Empty(diagnostics);
    }

    // SELECT clause scalar subquery - Invalid cases
    [Theory]
    [InlineData("SELECT (SELECT x.id FROM table1 t) FROM users u;", "x")]
    public void Analyze_SelectClauseSubquery_UndefinedAlias_ReturnsDiagnostic(string sql, string expectedAlias)
    {
        var rule = new UndefinedAliasRule();
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context)
            .Where(d => d.Data?.RuleId == "semantic/undefined-alias")
            .ToArray();

        Assert.Single(diagnostics);
        Assert.Contains(expectedAlias, diagnostics[0].Message);
    }

    // WHERE EXISTS subquery - Valid cases (including correlated)
    [Theory]
    [InlineData("SELECT u.id FROM users u WHERE EXISTS (SELECT 1 FROM orders o WHERE o.user_id = u.id);")]
    [InlineData("SELECT * FROM users u WHERE NOT EXISTS (SELECT o.id FROM orders o WHERE o.user_id = u.id);")]
    [InlineData("SELECT u.id FROM users u WHERE EXISTS (SELECT o.id FROM orders o);")]  // non-correlated
    public void Analyze_WhereExistsSubquery_ValidAliases_ReturnsEmpty(string sql)
    {
        var rule = new UndefinedAliasRule();
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context)
            .Where(d => d.Data?.RuleId == "semantic/undefined-alias")
            .ToArray();

        Assert.Empty(diagnostics);
    }

    // WHERE EXISTS subquery - Invalid cases
    [Theory]
    [InlineData("SELECT u.id FROM users u WHERE EXISTS (SELECT x.id FROM orders o);", "x")]
    public void Analyze_WhereExistsSubquery_UndefinedAlias_ReturnsDiagnostic(string sql, string expectedAlias)
    {
        var rule = new UndefinedAliasRule();
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context)
            .Where(d => d.Data?.RuleId == "semantic/undefined-alias")
            .ToArray();

        Assert.Single(diagnostics);
        Assert.Contains(expectedAlias, diagnostics[0].Message);
    }

    // WHERE IN subquery - Valid cases
    [Theory]
    [InlineData("SELECT u.id FROM users u WHERE u.id IN (SELECT o.user_id FROM orders o);")]
    [InlineData("SELECT u.id FROM users u WHERE u.id NOT IN (SELECT o.user_id FROM orders o);")]
    public void Analyze_WhereInSubquery_ValidAliases_ReturnsEmpty(string sql)
    {
        var rule = new UndefinedAliasRule();
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context)
            .Where(d => d.Data?.RuleId == "semantic/undefined-alias")
            .ToArray();

        Assert.Empty(diagnostics);
    }

    // WHERE IN subquery - Invalid cases
    [Theory]
    [InlineData("SELECT u.id FROM users u WHERE u.id IN (SELECT x.id FROM orders o);", "x")]
    public void Analyze_WhereInSubquery_UndefinedAlias_ReturnsDiagnostic(string sql, string expectedAlias)
    {
        var rule = new UndefinedAliasRule();
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context)
            .Where(d => d.Data?.RuleId == "semantic/undefined-alias")
            .ToArray();

        Assert.Single(diagnostics);
        Assert.Contains(expectedAlias, diagnostics[0].Message);
    }

    // Deeply nested subqueries - Valid
    [Fact]
    public void Analyze_DeeplyNestedSubquery_ValidAliases_ReturnsEmpty()
    {
        var rule = new UndefinedAliasRule();
        var sql = @"
            SELECT a.id
            FROM (
                SELECT b.id
                FROM (
                    SELECT c.id FROM table1 c
                ) AS b
            ) AS a;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context)
            .Where(d => d.Data?.RuleId == "semantic/undefined-alias")
            .ToArray();

        Assert.Empty(diagnostics);
    }

    // Deeply nested subqueries - Invalid
    [Fact]
    public void Analyze_DeeplyNestedSubquery_UndefinedAlias_ReturnsDiagnostic()
    {
        var rule = new UndefinedAliasRule();
        var sql = @"
            SELECT a.id
            FROM (
                SELECT x.id
                FROM (
                    SELECT c.id FROM table1 c
                ) AS b
            ) AS a;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context)
            .Where(d => d.Data?.RuleId == "semantic/undefined-alias")
            .ToArray();

        Assert.Single(diagnostics);
        Assert.Contains("x", diagnostics[0].Message);
    }

    // Correlated subquery with multiple outer references
    [Fact]
    public void Analyze_CorrelatedSubquery_MultipleOuterReferences_NoError()
    {
        var rule = new UndefinedAliasRule();
        var sql = @"
            SELECT u.name
            FROM users u
            JOIN departments d ON u.dept_id = d.id
            WHERE EXISTS (
                SELECT 1
                FROM orders o
                WHERE o.user_id = u.id AND o.dept_id = d.id
            );";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context)
            .Where(d => d.Data?.RuleId == "semantic/undefined-alias")
            .ToArray();

        Assert.Empty(diagnostics);
    }

    // UNION in subquery - Valid
    [Fact]
    public void Analyze_UnionInSubquery_ValidAliases_ReturnsEmpty()
    {
        var rule = new UndefinedAliasRule();
        var sql = @"
            SELECT *
            FROM (
                SELECT t1.id FROM table1 t1
                UNION
                SELECT t2.id FROM table2 t2
            ) AS combined;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context)
            .Where(d => d.Data?.RuleId == "semantic/undefined-alias")
            .ToArray();

        Assert.Empty(diagnostics);
    }

    // UNION in subquery - Invalid
    [Fact]
    public void Analyze_UnionInSubquery_UndefinedAlias_ReturnsDiagnostic()
    {
        var rule = new UndefinedAliasRule();
        var sql = @"
            SELECT *
            FROM (
                SELECT t1.id FROM table1 t1
                UNION
                SELECT x.id FROM table2 t2
            ) AS combined;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context)
            .Where(d => d.Data?.RuleId == "semantic/undefined-alias")
            .ToArray();

        Assert.Single(diagnostics);
        Assert.Contains("x", diagnostics[0].Message);
    }

    #endregion
}
