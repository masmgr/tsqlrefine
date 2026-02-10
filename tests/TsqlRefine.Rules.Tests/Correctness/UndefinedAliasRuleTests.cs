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

    [Theory]
    [InlineData("SELECT t.col FROM #temp t;")]  // temp table with alias
    [InlineData("SELECT #temp.col FROM #temp;")]  // temp table without alias (implicit name)
    [InlineData("SELECT t.id FROM #temp t JOIN users u ON t.id = u.id;")]
    [InlineData("SELECT t.col FROM ##global_temp t;")]  // global temp table
    public void Analyze_WithTemporaryTable_RecognizesAlias(string sql)
    {
        var rule = new UndefinedAliasRule();
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context)
            .Where(d => d.Data?.RuleId == "semantic/undefined-alias")
            .ToArray();

        Assert.Empty(diagnostics);
    }

    [Theory]
    [InlineData("SELECT x.col FROM #temp t;", "x")]
    [InlineData("SELECT t.col, y.other FROM #temp t;", "y")]
    public void Analyze_WithTemporaryTableAndUndefinedAlias_ReturnsDiagnostic(string sql, string expectedAlias)
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
    [InlineData("SELECT tv.col FROM @tableVar tv;")]  // table variable with alias
    [InlineData("SELECT @tableVar.col FROM @tableVar;")]  // table variable without alias (implicit name)
    [InlineData("SELECT tv.id FROM @tableVar tv JOIN users u ON tv.id = u.id;")]
    public void Analyze_WithTableVariable_RecognizesAlias(string sql)
    {
        var rule = new UndefinedAliasRule();
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context)
            .Where(d => d.Data?.RuleId == "semantic/undefined-alias")
            .ToArray();

        Assert.Empty(diagnostics);
    }

    [Theory]
    [InlineData("SELECT x.col FROM @tableVar tv;", "x")]
    [InlineData("SELECT tv.col, y.other FROM @tableVar tv;", "y")]
    public void Analyze_WithTableVariableAndUndefinedAlias_ReturnsDiagnostic(string sql, string expectedAlias)
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

    // Correlated subquery referencing outer table without alias
    [Theory]
    [InlineData("SELECT (SELECT TOP 1 T2.NAME FROM T2 WHERE T2.ID_T1 = T1.ID) AS T2_TOP FROM T1;")]
    [InlineData("SELECT T1.ID FROM T1 WHERE EXISTS(SELECT * FROM T2 WHERE T2.ID_T1 = T1.ID);")]
    [InlineData("SELECT T1.ID FROM T1 WHERE T1.ID IN (SELECT T2.ID_T1 FROM T2 WHERE T2.ACTIVE = 1);")]
    public void Analyze_CorrelatedSubquery_OuterTableWithoutAlias_ReturnsEmpty(string sql)
    {
        var rule = new UndefinedAliasRule();
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context)
            .Where(d => d.Data?.RuleId == "semantic/undefined-alias")
            .ToArray();

        Assert.Empty(diagnostics);
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

    #region INSERT Statement Tests

    // INSERT ... SELECT with correlated subquery
    [Theory]
    [InlineData(@"INSERT INTO target (col1) SELECT (SELECT MAX(t2.value) FROM source t2 WHERE t2.id = t1.id) FROM source t1;")]
    [InlineData(@"INSERT INTO target (col1, col2) SELECT t1.id, (SELECT COUNT(*) FROM items t2 WHERE t2.category = t1.category) FROM items t1;")]
    public void Analyze_InsertSelectWithCorrelatedSubquery_ValidAliases_ReturnsEmpty(string sql)
    {
        var rule = new UndefinedAliasRule();
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context)
            .Where(d => d.Data?.RuleId == "semantic/undefined-alias")
            .ToArray();

        Assert.Empty(diagnostics);
    }

    // INSERT ... SELECT with undefined alias
    [Theory]
    [InlineData(@"INSERT INTO target (col1) SELECT (SELECT MAX(t2.value) FROM source t2 WHERE t2.id = x.id) FROM source t1;", "x")]
    public void Analyze_InsertSelectWithCorrelatedSubquery_UndefinedAlias_ReturnsDiagnostic(string sql, string expectedAlias)
    {
        var rule = new UndefinedAliasRule();
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context)
            .Where(d => d.Data?.RuleId == "semantic/undefined-alias")
            .ToArray();

        Assert.Single(diagnostics);
        Assert.Contains(expectedAlias, diagnostics[0].Message);
    }

    #endregion

    #region UPDATE/DELETE Statement Tests

    // UPDATE with correlated subquery referencing FROM clause alias
    [Theory]
    [InlineData(@"UPDATE #t SET seq = (SELECT COUNT(*) FROM #t t2 WHERE t1.id = t2.id AND t1.code >= t2.code) FROM #t t1;")]
    [InlineData(@"UPDATE t SET t.value = (SELECT MAX(s.value) FROM source s WHERE s.id = t.id) FROM target t;")]
    [InlineData(@"UPDATE items SET rank = (SELECT COUNT(*) FROM items i2 WHERE i1.category = i2.category AND i1.price >= i2.price) FROM items i1;")]
    public void Analyze_UpdateWithCorrelatedSubquery_ValidAliases_ReturnsEmpty(string sql)
    {
        var rule = new UndefinedAliasRule();
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context)
            .Where(d => d.Data?.RuleId == "semantic/undefined-alias")
            .ToArray();

        Assert.Empty(diagnostics);
    }

    // UPDATE with undefined alias in correlated subquery
    [Theory]
    [InlineData(@"UPDATE t SET t.value = (SELECT MAX(s.value) FROM source s WHERE x.id = s.id) FROM target t;", "x")]
    public void Analyze_UpdateWithCorrelatedSubquery_UndefinedAlias_ReturnsDiagnostic(string sql, string expectedAlias)
    {
        var rule = new UndefinedAliasRule();
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context)
            .Where(d => d.Data?.RuleId == "semantic/undefined-alias")
            .ToArray();

        Assert.Single(diagnostics);
        Assert.Contains(expectedAlias, diagnostics[0].Message);
    }

    // DELETE with correlated subquery
    [Theory]
    [InlineData(@"DELETE t FROM target t WHERE EXISTS (SELECT 1 FROM source s WHERE s.id = t.id);")]
    [InlineData(@"DELETE FROM target WHERE id IN (SELECT s.target_id FROM source s);")]
    public void Analyze_DeleteWithCorrelatedSubquery_ValidAliases_ReturnsEmpty(string sql)
    {
        var rule = new UndefinedAliasRule();
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context)
            .Where(d => d.Data?.RuleId == "semantic/undefined-alias")
            .ToArray();

        Assert.Empty(diagnostics);
    }

    // DELETE with undefined alias
    [Theory]
    [InlineData(@"DELETE t FROM target t WHERE EXISTS (SELECT 1 FROM source s WHERE x.id = s.id);", "x")]
    public void Analyze_DeleteWithCorrelatedSubquery_UndefinedAlias_ReturnsDiagnostic(string sql, string expectedAlias)
    {
        var rule = new UndefinedAliasRule();
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context)
            .Where(d => d.Data?.RuleId == "semantic/undefined-alias")
            .ToArray();

        Assert.Single(diagnostics);
        Assert.Contains(expectedAlias, diagnostics[0].Message);
    }

    #endregion

    #region Built-in Table-Valued Function Tests

    // STRING_SPLIT - Valid cases
    [Theory]
    [InlineData("SELECT s.value FROM STRING_SPLIT('a,b,c', ',') AS s;")]
    [InlineData("SELECT s.value, s.ordinal FROM STRING_SPLIT('a,b,c', ',', 1) AS s;")]
    [InlineData("SELECT t.id, s.value FROM table1 t CROSS APPLY STRING_SPLIT(t.csv, ',') AS s;")]
    [InlineData("SELECT t.id, s.value FROM table1 t OUTER APPLY STRING_SPLIT(t.csv, ',') AS s;")]
    public void Analyze_WithStringSplit_ValidAliases_ReturnsEmpty(string sql)
    {
        var rule = new UndefinedAliasRule();
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context)
            .Where(d => d.Data?.RuleId == "semantic/undefined-alias")
            .ToArray();

        Assert.Empty(diagnostics);
    }

    // STRING_SPLIT - Invalid cases
    [Theory]
    [InlineData("SELECT x.value FROM STRING_SPLIT('a,b,c', ',') AS s;", "x")]
    [InlineData("SELECT s.value, y.other FROM STRING_SPLIT('a,b,c', ',') AS s;", "y")]
    public void Analyze_WithStringSplitAndUndefinedAlias_ReturnsDiagnostic(string sql, string expectedAlias)
    {
        var rule = new UndefinedAliasRule();
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context)
            .Where(d => d.Data?.RuleId == "semantic/undefined-alias")
            .ToArray();

        Assert.Single(diagnostics);
        Assert.Contains(expectedAlias, diagnostics[0].Message);
    }

    // OPENJSON - Valid cases
    [Theory]
    [InlineData("SELECT j.value FROM OPENJSON(@json) AS j;")]
    [InlineData("SELECT j.[key], j.value FROM OPENJSON('[1,2,3]') AS j;")]
    [InlineData("SELECT t.id, j.value FROM table1 t CROSS APPLY OPENJSON(t.json_col) AS j;")]
    public void Analyze_WithOpenJson_ValidAliases_ReturnsEmpty(string sql)
    {
        var rule = new UndefinedAliasRule();
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context)
            .Where(d => d.Data?.RuleId == "semantic/undefined-alias")
            .ToArray();

        Assert.Empty(diagnostics);
    }

    // OPENJSON - Invalid cases
    [Theory]
    [InlineData("SELECT x.value FROM OPENJSON(@json) AS j;", "x")]
    public void Analyze_WithOpenJsonAndUndefinedAlias_ReturnsDiagnostic(string sql, string expectedAlias)
    {
        var rule = new UndefinedAliasRule();
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context)
            .Where(d => d.Data?.RuleId == "semantic/undefined-alias")
            .ToArray();

        Assert.Single(diagnostics);
        Assert.Contains(expectedAlias, diagnostics[0].Message);
    }

    // VALUES clause as table (InlineDerivedTable) - Valid cases
    [Theory]
    [InlineData("SELECT v.id FROM (VALUES (1), (2)) AS v(id);")]
    [InlineData("SELECT v.id, v.name FROM (VALUES (1, 'a'), (2, 'b')) AS v(id, name);")]
    [InlineData("SELECT t.col, v.id FROM table1 t JOIN (VALUES (1), (2)) AS v(id) ON t.id = v.id;")]
    public void Analyze_WithValuesClause_ValidAliases_ReturnsEmpty(string sql)
    {
        var rule = new UndefinedAliasRule();
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context)
            .Where(d => d.Data?.RuleId == "semantic/undefined-alias")
            .ToArray();

        Assert.Empty(diagnostics);
    }

    // VALUES clause - Invalid cases
    [Theory]
    [InlineData("SELECT x.id FROM (VALUES (1), (2)) AS v(id);", "x")]
    public void Analyze_WithValuesClauseAndUndefinedAlias_ReturnsDiagnostic(string sql, string expectedAlias)
    {
        var rule = new UndefinedAliasRule();
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context)
            .Where(d => d.Data?.RuleId == "semantic/undefined-alias")
            .ToArray();

        Assert.Single(diagnostics);
        Assert.Contains(expectedAlias, diagnostics[0].Message);
    }

    // GENERATE_SERIES - Valid cases (SQL Server 2022+)
    [Theory]
    [InlineData("SELECT g.value FROM GENERATE_SERIES(1, 10) AS g;")]
    [InlineData("SELECT g.value FROM GENERATE_SERIES(1, 100, 5) AS g;")]
    public void Analyze_WithGenerateSeries_ValidAliases_ReturnsEmpty(string sql)
    {
        var rule = new UndefinedAliasRule();
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context)
            .Where(d => d.Data?.RuleId == "semantic/undefined-alias")
            .ToArray();

        Assert.Empty(diagnostics);
    }

    #endregion

    #region APPLY Argument Tests

    [Theory]
    [InlineData("SELECT t.id, s.value FROM table1 t CROSS APPLY STRING_SPLIT(t.csv, ',') AS s;")]
    [InlineData("SELECT t.id, j.value FROM table1 t OUTER APPLY OPENJSON(t.json_col) AS j;")]
    [InlineData("SELECT t.id, f.value FROM table1 t CROSS APPLY dbo.f_func(t.id) AS f;")]
    public void Analyze_WithApplyArguments_ValidAliases_ReturnsEmpty(string sql)
    {
        var rule = new UndefinedAliasRule();
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context)
            .Where(d => d.Data?.RuleId == "semantic/undefined-alias")
            .ToArray();

        Assert.Empty(diagnostics);
    }

    [Theory]
    [InlineData("SELECT t.id, s.value FROM table1 t CROSS APPLY STRING_SPLIT(x.csv, ',') AS s;", "x")]
    [InlineData("SELECT t.id, j.value FROM table1 t OUTER APPLY OPENJSON(x.json_col) AS j;", "x")]
    [InlineData("SELECT t.id, f.value FROM table1 t CROSS APPLY dbo.f_func(x.id) AS f;", "x")]
    public void Analyze_WithApplyArguments_UndefinedAlias_ReturnsDiagnostic(string sql, string expectedAlias)
    {
        var rule = new UndefinedAliasRule();
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context)
            .Where(d => d.Data?.RuleId == "semantic/undefined-alias")
            .ToArray();

        Assert.Single(diagnostics);
        Assert.Contains(expectedAlias, diagnostics[0].Message);
    }

    #endregion

    #region OUTPUT Clause Tests

    [Theory]
    [InlineData("UPDATE t SET t.value = t.value + 1 OUTPUT inserted.id, deleted.id FROM target t;")]
    [InlineData("DELETE t OUTPUT deleted.id FROM target t;")]
    [InlineData("INSERT INTO target(id) OUTPUT inserted.id VALUES (1);")]
    [InlineData("UPDATE t SET t.value = 1 OUTPUT inserted.id INTO audit(id) FROM target t;")]
    public void Analyze_WithOutputClause_ValidAliases_ReturnsEmpty(string sql)
    {
        var rule = new UndefinedAliasRule();
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context)
            .Where(d => d.Data?.RuleId == "semantic/undefined-alias")
            .ToArray();

        Assert.Empty(diagnostics);
    }

    [Theory]
    [InlineData("UPDATE t SET t.value = t.value + 1 OUTPUT x.id FROM target t;", "x")]
    [InlineData("DELETE t OUTPUT x.id FROM target t;", "x")]
    [InlineData("INSERT INTO target(id) OUTPUT x.id VALUES (1);", "x")]
    [InlineData("UPDATE t SET t.value = 1 OUTPUT x.id INTO audit(id) FROM target t;", "x")]
    public void Analyze_WithOutputClause_UndefinedAlias_ReturnsDiagnostic(string sql, string expectedAlias)
    {
        var rule = new UndefinedAliasRule();
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context)
            .Where(d => d.Data?.RuleId == "semantic/undefined-alias")
            .ToArray();

        Assert.Single(diagnostics);
        Assert.Contains(expectedAlias, diagnostics[0].Message);
    }

    #endregion

    #region MERGE Statement Tests

    [Theory]
    [InlineData("MERGE target t USING source s ON t.id = s.id WHEN MATCHED THEN UPDATE SET t.value = s.value;")]
    [InlineData("MERGE target USING source s ON target.id = s.id WHEN MATCHED THEN UPDATE SET value = s.value;")]
    [InlineData("MERGE target t USING source s ON t.id = s.id WHEN MATCHED THEN UPDATE SET t.value = s.value OUTPUT inserted.id, deleted.id, s.id;")]
    public void Analyze_WithMerge_ValidAliases_ReturnsEmpty(string sql)
    {
        var rule = new UndefinedAliasRule();
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context)
            .Where(d => d.Data?.RuleId == "semantic/undefined-alias")
            .ToArray();

        Assert.Empty(diagnostics);
    }

    [Theory]
    [InlineData("MERGE target t USING source s ON x.id = s.id WHEN MATCHED THEN UPDATE SET t.value = s.value;", "x")]
    [InlineData("MERGE target t USING source s ON t.id = s.id WHEN MATCHED THEN UPDATE SET t.value = x.value;", "x")]
    [InlineData("MERGE target t USING source s ON t.id = s.id WHEN MATCHED THEN UPDATE SET t.value = s.value OUTPUT x.id;", "x")]
    public void Analyze_WithMerge_UndefinedAlias_ReturnsDiagnostic(string sql, string expectedAlias)
    {
        var rule = new UndefinedAliasRule();
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context)
            .Where(d => d.Data?.RuleId == "semantic/undefined-alias")
            .ToArray();

        Assert.Single(diagnostics);
        Assert.Contains(expectedAlias, diagnostics[0].Message);
    }

    #endregion

    #region CTE Tests

    // CTE - Valid alias in CTE definition
    [Theory]
    [InlineData("WITH CTE AS (SELECT u.id FROM users u) SELECT * FROM CTE;")]
    [InlineData("WITH CTE AS (SELECT id FROM users) SELECT c.id FROM CTE c;")]
    [InlineData("WITH CTE AS (SELECT id FROM users) SELECT CTE.id FROM CTE;")]
    public void Analyze_WithCte_ValidAliases_ReturnsEmpty(string sql)
    {
        var rule = new UndefinedAliasRule();
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context)
            .Where(d => d.Data?.RuleId == "semantic/undefined-alias")
            .ToArray();

        Assert.Empty(diagnostics);
    }

    // CTE - Undefined alias in CTE definition
    [Theory]
    [InlineData("WITH CTE AS (SELECT x.id FROM users) SELECT * FROM CTE;", "x")]
    [InlineData("WITH CTE AS (SELECT u.id FROM users u WHERE x.status = 1) SELECT * FROM CTE;", "x")]
    public void Analyze_WithCte_UndefinedAliasInDefinition_ReturnsDiagnostic(string sql, string expectedAlias)
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
    [InlineData("WITH CTE AS (SELECT x.id FROM users) UPDATE target SET id = 1;", "x")]
    [InlineData("WITH CTE AS (SELECT x.id FROM users) DELETE FROM target;", "x")]
    [InlineData("WITH CTE AS (SELECT x.id FROM users) INSERT INTO target(id) SELECT id FROM CTE;", "x")]
    [InlineData("WITH CTE AS (SELECT x.id FROM users) MERGE target t USING source s ON t.id = s.id WHEN MATCHED THEN UPDATE SET t.id = s.id;", "x")]
    public void Analyze_WithCteInDml_UndefinedAliasInDefinition_ReturnsDiagnostic(string sql, string expectedAlias)
    {
        var rule = new UndefinedAliasRule();
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context)
            .Where(d => d.Data?.RuleId == "semantic/undefined-alias")
            .ToArray();

        Assert.Single(diagnostics);
        Assert.Contains(expectedAlias, diagnostics[0].Message);
    }

    // Multiple CTEs (chained)
    [Theory]
    [InlineData("WITH CTE1 AS (SELECT id FROM users), CTE2 AS (SELECT c1.id FROM CTE1 c1) SELECT * FROM CTE2;")]
    [InlineData("WITH CTE1 AS (SELECT id FROM users), CTE2 AS (SELECT CTE1.id FROM CTE1) SELECT c2.id FROM CTE2 c2;")]
    public void Analyze_WithMultipleCtes_ValidReferences_ReturnsEmpty(string sql)
    {
        var rule = new UndefinedAliasRule();
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context)
            .Where(d => d.Data?.RuleId == "semantic/undefined-alias")
            .ToArray();

        Assert.Empty(diagnostics);
    }

    // CTE - Undefined alias in second CTE (not forward reference, which is a different semantic issue)
    [Fact]
    public void Analyze_WithMultipleCtes_UndefinedAliasInSecondCte_ReturnsDiagnostic()
    {
        var sql = "WITH CTE1 AS (SELECT id FROM users), CTE2 AS (SELECT x.id FROM CTE1) SELECT * FROM CTE2;";
        var rule = new UndefinedAliasRule();
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context)
            .Where(d => d.Data?.RuleId == "semantic/undefined-alias")
            .ToArray();

        Assert.Single(diagnostics);
        Assert.Contains("x", diagnostics[0].Message);
    }

    // Recursive CTE - Valid self-reference
    [Theory]
    [InlineData(@"
        WITH RecursiveCTE AS (
            SELECT id, parent_id FROM categories WHERE parent_id IS NULL
            UNION ALL
            SELECT c.id, c.parent_id FROM categories c
            INNER JOIN RecursiveCTE r ON c.parent_id = r.id
        )
        SELECT * FROM RecursiveCTE;")]
    [InlineData(@"
        WITH RecursiveCTE AS (
            SELECT id FROM items WHERE parent_id IS NULL
            UNION ALL
            SELECT i.id FROM items i, RecursiveCTE
            WHERE i.parent_id = RecursiveCTE.id
        )
        SELECT r.id FROM RecursiveCTE r;")]
    public void Analyze_WithRecursiveCte_ValidSelfReference_ReturnsEmpty(string sql)
    {
        var rule = new UndefinedAliasRule();
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context)
            .Where(d => d.Data?.RuleId == "semantic/undefined-alias")
            .ToArray();

        Assert.Empty(diagnostics);
    }

    // Recursive CTE - Undefined alias in recursive part
    [Fact]
    public void Analyze_WithRecursiveCte_UndefinedAliasInRecursivePart_ReturnsDiagnostic()
    {
        var sql = @"
            WITH RecursiveCTE AS (
                SELECT id FROM categories WHERE parent_id IS NULL
                UNION ALL
                SELECT x.id FROM categories c
                INNER JOIN RecursiveCTE r ON c.parent_id = r.id
            )
            SELECT * FROM RecursiveCTE;";
        var rule = new UndefinedAliasRule();
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context)
            .Where(d => d.Data?.RuleId == "semantic/undefined-alias")
            .ToArray();

        Assert.Single(diagnostics);
        Assert.Contains("x", diagnostics[0].Message);
    }

    // CTE with subquery inside CTE definition
    [Fact]
    public void Analyze_WithCte_SubqueryInsideCteDefinition_ValidAliases_ReturnsEmpty()
    {
        var sql = @"
            WITH CTE AS (
                SELECT u.id FROM users u
                WHERE u.status = (SELECT MAX(status) FROM statuses)
            )
            SELECT c.id FROM CTE c;";
        var rule = new UndefinedAliasRule();
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context)
            .Where(d => d.Data?.RuleId == "semantic/undefined-alias")
            .ToArray();

        Assert.Empty(diagnostics);
    }

    // CTE - Main query subquery can reference CTE
    [Fact]
    public void Analyze_WithCte_SubqueryInMainQueryReferencesCte_ReturnsEmpty()
    {
        var sql = @"
            WITH MyCTE AS (SELECT id FROM users)
            SELECT * FROM products p
            WHERE p.user_id IN (SELECT c.id FROM MyCTE c);";
        var rule = new UndefinedAliasRule();
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context)
            .Where(d => d.Data?.RuleId == "semantic/undefined-alias")
            .ToArray();

        Assert.Empty(diagnostics);
    }

    #endregion
}
