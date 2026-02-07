using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Style;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Style;

public sealed class JoinKeywordRuleTests
{
    private readonly JoinKeywordRule _rule = new();

    private static RuleContext CreateContext(string sql)
    {
        return RuleTestContext.CreateContext(sql);
    }

    [Fact]
    public void Analyze_CommaJoin_ReturnsDiagnostic()
    {
        // Arrange
        var sql = "SELECT * FROM users, profiles;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("join-keyword", diagnostic.Code);
        Assert.Contains("implicit join", diagnostic.Message.ToLowerInvariant());
        Assert.Contains("INNER JOIN", diagnostic.Message);
    }

    [Fact]
    public void Analyze_MultipleCommaJoins_ReturnsDiagnostics()
    {
        // Arrange - Each comma is reported separately
        var sql = "SELECT * FROM users, profiles, departments;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        Assert.Equal(2, diagnostics.Count);
        Assert.All(diagnostics, d => Assert.Equal("join-keyword", d.Code));
    }

    [Fact]
    public void Analyze_InnerJoin_NoDiagnostic()
    {
        // Arrange
        var sql = "SELECT * FROM users INNER JOIN profiles ON users.id = profiles.user_id;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_LeftJoin_NoDiagnostic()
    {
        // Arrange
        var sql = "SELECT * FROM users LEFT JOIN profiles ON users.id = profiles.user_id;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_SingleTable_NoDiagnostic()
    {
        // Arrange
        var sql = "SELECT * FROM users;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_CrossJoin_NoDiagnostic()
    {
        // Arrange - CROSS JOIN is explicit
        var sql = "SELECT * FROM users CROSS JOIN profiles;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_CommaJoinInSubquery_ReturnsDiagnostic()
    {
        // Arrange
        var sql = @"
            SELECT *
            FROM (
                SELECT * FROM users, profiles
            ) AS subquery;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("join-keyword", diagnostic.Code);
    }

    [Fact]
    public void Analyze_CommaJoinWithWhere_ReturnsDiagnostic()
    {
        // Arrange
        var sql = "SELECT * FROM users, profiles WHERE users.id = profiles.user_id;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("join-keyword", diagnostic.Code);
    }

    [Fact]
    public void Analyze_MixedJoinTypes_ReturnsDiagnostic()
    {
        // Arrange
        var sql = @"
            SELECT *
            FROM users, profiles
            INNER JOIN departments ON users.dept_id = departments.id;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("join-keyword", diagnostic.Code);
    }

    [Fact]
    public void Analyze_MultipleInnerJoins_NoDiagnostic()
    {
        // Arrange
        var sql = @"
            SELECT *
            FROM users
            INNER JOIN profiles ON users.id = profiles.user_id
            INNER JOIN departments ON users.dept_id = departments.id;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_CommaInCTE_ReturnsDiagnostic()
    {
        // Arrange
        var sql = @"
            WITH cte AS (
                SELECT * FROM users, profiles
            )
            SELECT * FROM cte;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("join-keyword", diagnostic.Code);
    }

    [Fact]
    public void GetFixes_ReturnsNoFixes()
    {
        // Arrange
        var sql = "SELECT * FROM users, profiles;";
        var context = CreateContext(sql);
        var diagnostic = _rule.Analyze(context).First();

        // Act
        var fixes = _rule.GetFixes(context, diagnostic);

        // Assert
        Assert.Empty(fixes);
    }

    [Fact]
    public void Metadata_HasCorrectProperties()
    {
        // Assert
        Assert.Equal("join-keyword", _rule.Metadata.RuleId);
        Assert.Equal("Style", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Warning, _rule.Metadata.DefaultSeverity);
        Assert.False(_rule.Metadata.Fixable);
    }

    /// <summary>
    /// Verifies that commas in UPDATE SET clause are not flagged as comma joins.
    /// SET clause commas appear before FROM clause and should be excluded from comma join detection.
    /// </summary>
    [Fact]
    public void Analyze_UpdateWithSetClauseAndInnerJoin_NoDiagnostic()
    {
        // Arrange - SET clause commas should not be flagged as comma joins
        var sql = @"
UPDATE t
SET
    col1 = ISNULL(#temp.col1, $0),
    col2 = #temp.col2
FROM
    t
    INNER JOIN #temp ON #temp.id = t.id
WHERE
    t.status = 1;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        Assert.Empty(diagnostics);
    }

    /// <summary>
    /// Verifies that scanning from FROM inside a subquery does not leak to outer UPDATE SET clause.
    /// After the subquery closes with a parenthesis, commas in outer SQL statements should not be flagged.
    /// </summary>
    [Fact]
    public void Analyze_SubqueryFromDoesNotLeakToOuterSetClause_NoDiagnostic()
    {
        // Arrange - FROM in subquery should not cause scanning to leak to outer UPDATE SET clause
        var sql = @"
SELECT * FROM (
    SELECT * FROM #temp
) AS subquery;

UPDATE #temp
SET
    col1 = ISNULL(x, $0),
    col2 = y
FROM
    #temp
    INNER JOIN #other ON #other.id = #temp.id;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        Assert.Empty(diagnostics);
    }

    /// <summary>
    /// Verifies that scanning from FROM inside a derived table (subquery in INNER JOIN) does not leak
    /// to subsequent UPDATE SET clause. When a subquery is used as a join target in FROM clause,
    /// the FROM inside that subquery should not affect subsequent statements.
    /// </summary>
    [Fact]
    public void Analyze_DerivedTableJoinFromDoesNotLeakToOuterSetClause_NoDiagnostic()
    {
        // Arrange - FROM inside derived table join should not leak to outer UPDATE SET clause
        var sql = @"
SELECT *
FROM
    #temp
    INNER JOIN
    (
        SELECT sort_order, ROW_NUMBER() OVER (ORDER BY x DESC) AS row_num
        FROM #temp
    ) AS priority ON priority.sort_order = #temp.sort_order;

UPDATE #temp
SET
    col1 = ISNULL(x, $0),
    col2 = y
FROM
    #temp
    INNER JOIN #other ON #other.id = #temp.id;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        Assert.Empty(diagnostics);
    }

    /// <summary>
    /// Verifies that commas in SET clause are not flagged when multiple UPDATE statements are consecutive.
    /// Scanning from the first UPDATE's FROM clause should not leak to the next UPDATE's SET clause.
    /// This is handled by recognizing SET keyword as a new statement start.
    /// </summary>
    [Fact]
    public void Analyze_MultipleUpdateStatementsWithInnerJoin_NoDiagnostic()
    {
        // Arrange - Multiple UPDATE statements with INNER JOIN should not flag SET clause commas
        var sql = @"
UPDATE #temp
SET
    col1 = CASE WHEN x = 1 THEN y ELSE z END,
    col2 = CASE WHEN a IS NULL THEN b ELSE a END;

UPDATE target_table
SET
    col1 = #temp.col1,
    col2 = #temp.col2,
    col3 = ISNULL(#temp.col3, $0),
    col4 = ISNULL(#temp.col4, $0)
FROM
    target_table
    INNER JOIN #temp ON #temp.id = target_table.id
WHERE
    target_table.project_id = @project_id;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        Assert.Empty(diagnostics);
    }
}
