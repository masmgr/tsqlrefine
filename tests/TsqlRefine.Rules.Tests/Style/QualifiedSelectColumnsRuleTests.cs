using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Style;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Style;

public sealed class QualifiedSelectColumnsRuleTests
{
    private readonly QualifiedSelectColumnsRule _rule = new();

    [Theory]
    [InlineData("SELECT id FROM users u INNER JOIN orders o ON u.id = o.user_id;")]
    [InlineData("SELECT name FROM users, orders;")]
    [InlineData("SELECT email FROM users u LEFT JOIN orders o ON u.id = o.user_id;")]
    [InlineData("select id from users u, orders o;")]  // lowercase
    public void Analyze_WhenUnqualifiedColumnInMultiTableQuery_ReturnsDiagnostic(string sql)
    {
        // Arrange
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.NotEmpty(diagnostics);
        Assert.All(diagnostics, d => Assert.Equal("qualified-select-columns", d.Code));
    }

    [Theory]
    [InlineData("SELECT u.id FROM users u INNER JOIN orders o ON u.id = o.user_id;")]
    [InlineData("SELECT u.name, o.total FROM users u, orders o;")]
    [InlineData("SELECT users.id FROM users INNER JOIN orders ON users.id = orders.user_id;")]
    [InlineData("SELECT * FROM users;")]  // Single table
    [InlineData("SELECT id, name FROM users;")]  // Single table, unqualified is OK
    [InlineData("")]  // Empty
    public void Analyze_WhenValid_ReturnsEmpty(string sql)
    {
        // Arrange
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_MultipleUnqualifiedColumns_ReturnsMultipleDiagnostics()
    {
        // Arrange
        const string sql = @"
            SELECT id, name, total
            FROM users u
            INNER JOIN orders o ON u.id = o.user_id;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Equal(3, diagnostics.Length);
        Assert.All(diagnostics, d => Assert.Equal("qualified-select-columns", d.Code));
    }

    [Fact]
    public void Analyze_MixedQualifiedAndUnqualified_ReturnsOnlyUnqualified()
    {
        // Arrange
        const string sql = @"
            SELECT u.id, name
            FROM users u
            INNER JOIN orders o ON u.id = o.user_id;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
        Assert.Contains("name", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_SelectStarInMultiTable_ReturnsEmpty()
    {
        // Arrange
        const string sql = "SELECT * FROM users u INNER JOIN orders o ON u.id = o.user_id;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_UnqualifiedInExpression_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = @"
            SELECT UPPER(name) AS upper_name
            FROM users u
            INNER JOIN orders o ON u.id = o.user_id;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.NotEmpty(diagnostics);
        Assert.Contains("name", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_QualifiedInExpression_ReturnsEmpty()
    {
        // Arrange
        const string sql = @"
            SELECT UPPER(u.name) AS upper_name
            FROM users u
            INNER JOIN orders o ON u.id = o.user_id;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_DateAddDatePartLiteral_DoesNotReport()
    {
        // Arrange
        const string sql = @"
            SELECT DATEADD(day, 1, u.created_at) AS next_date
            FROM users u
            INNER JOIN orders o ON u.id = o.user_id;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_DateDiffBigDatePartLiteral_DoesNotReport()
    {
        // Arrange
        const string sql = @"
            SELECT DATEDIFF_BIG(day, u.created_at, GETDATE()) AS elapsed_days
            FROM users u
            INNER JOIN orders o ON u.id = o.user_id;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_InPredicateWithUnqualifiedValueList_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = @"
            SELECT CASE WHEN u.id IN (user_id) THEN 1 ELSE 0 END AS is_match
            FROM users u
            INNER JOIN orders o ON u.id = o.user_id;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
        Assert.Contains("user_id", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_InPredicateWithLiterals_ReturnsEmpty()
    {
        // Arrange
        const string sql = @"
            SELECT CASE WHEN u.id IN (1, 2, 3) THEN 1 ELSE 0 END AS is_match
            FROM users u
            INNER JOIN orders o ON u.id = o.user_id;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_DerivedTableWithMultipleTables_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = @"
            SELECT * FROM (
                SELECT id FROM users u, orders o
            ) AS subquery;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
        Assert.Contains("id", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_DerivedTableWithSingleTable_ReturnsEmpty()
    {
        // Arrange
        const string sql = @"
            SELECT * FROM (
                SELECT id FROM users
            ) AS subquery;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_CteWithMultipleTables_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = @"
            WITH cte AS (
                SELECT name FROM users u JOIN orders o ON u.id = o.user_id
            )
            SELECT cte.name FROM cte;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
        Assert.Contains("name", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_CteWithSingleTable_ReturnsEmpty()
    {
        // Arrange
        const string sql = @"
            WITH cte AS (
                SELECT id, name FROM users
            )
            SELECT cte.id, cte.name FROM cte;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_CrossApplyWithMultipleTables_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = @"
            SELECT t1.id, ca.col
            FROM t1
            CROSS APPLY (SELECT col FROM t2, t3) AS ca;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Contains(diagnostics, d => d.Message.Contains("col"));
    }

    [Fact]
    public void Analyze_OuterApplyQualified_ReturnsEmpty()
    {
        // Arrange
        const string sql = @"
            SELECT t1.id, ca.result
            FROM t1
            OUTER APPLY (SELECT t2.col AS result FROM t2) AS ca;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_CoalesceWithUnqualifiedColumn_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = @"
            SELECT COALESCE(name, 'N/A') AS display_name
            FROM users u
            INNER JOIN orders o ON u.id = o.user_id;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
        Assert.Contains("name", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_CoalesceWithQualifiedColumns_ReturnsEmpty()
    {
        // Arrange
        const string sql = @"
            SELECT COALESCE(u.name, o.description, 'N/A') AS display
            FROM users u
            INNER JOIN orders o ON u.id = o.user_id;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_NullIfWithUnqualifiedColumn_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = @"
            SELECT NULLIF(status, 0) AS active_status
            FROM users u
            INNER JOIN orders o ON u.id = o.user_id;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
        Assert.Contains("status", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_TryCastWithUnqualifiedColumn_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = @"
            SELECT TRY_CAST(amount AS decimal(10,2)) AS safe_amount
            FROM users u
            INNER JOIN orders o ON u.id = o.user_id;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
        Assert.Contains("amount", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_TryConvertWithUnqualifiedColumn_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = @"
            SELECT TRY_CONVERT(int, quantity) AS safe_qty
            FROM users u
            INNER JOIN orders o ON u.id = o.user_id;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
        Assert.Contains("quantity", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_BetweenWithUnqualifiedColumns_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = @"
            SELECT CASE WHEN price BETWEEN min_price AND max_price THEN 1 ELSE 0 END AS in_range
            FROM products p
            INNER JOIN categories c ON p.category_id = c.id;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Equal(3, diagnostics.Length);
    }

    [Fact]
    public void Analyze_SelectSetVariableUnqualified_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = @"
            DECLARE @val int;
            SELECT @val = amount
            FROM users u
            INNER JOIN orders o ON u.id = o.user_id;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
        Assert.Contains("amount", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_SelectSetVariableQualified_ReturnsEmpty()
    {
        // Arrange
        const string sql = @"
            DECLARE @val int;
            SELECT @val = o.amount
            FROM users u
            INNER JOIN orders o ON u.id = o.user_id;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_SingleTableQuery_ReturnsEmpty()
    {
        // Arrange
        const string sql = "SELECT id, name, email FROM users WHERE active = 1;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_EmptyInput_ReturnsEmpty()
    {
        // Arrange
        var context = CreateContext("");

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Metadata_HasCorrectProperties()
    {
        // Assert
        Assert.Equal("qualified-select-columns", _rule.Metadata.RuleId);
        Assert.Equal("Style", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Information, _rule.Metadata.DefaultSeverity);
        Assert.False(_rule.Metadata.Fixable);
    }

    [Fact]
    public void GetFixes_ReturnsEmpty()
    {
        // Arrange
        var context = CreateContext("SELECT id FROM users u, orders o;");
        var diagnostic = new Diagnostic(
            Range: new TsqlRefine.PluginSdk.Range(new Position(0, 0), new Position(0, 10)),
            Message: "test",
            Code: "qualified-select-columns"
        );

        // Act
        var fixes = _rule.GetFixes(context, diagnostic).ToArray();

        // Assert
        Assert.Empty(fixes);
    }

    private static RuleContext CreateContext(string sql)
    {
        return RuleTestContext.CreateContext(sql);
    }
}
