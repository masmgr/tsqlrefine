using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules;
using TsqlRefine.Rules.Rules.Performance;
using TsqlRefine.Rules.Tests.Helpers;
using Xunit;

namespace TsqlRefine.Rules.Tests.Performance;

public sealed class NonSargableRuleTests
{
    private readonly NonSargableRule _rule = new();



    [Fact]
    public void Analyze_FunctionOnColumnInWhere_ReturnsDiagnostic()
    {
        // Arrange
        var sql = "SELECT * FROM users WHERE LTRIM(username) = 'admin';";
        var context = RuleTestContext.CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("non-sargable", diagnostic.Code);
        Assert.Contains("LTRIM", diagnostic.Message);
        Assert.Contains("index", diagnostic.Message.ToLowerInvariant());
    }

    [Fact]
    public void Analyze_DatePartInWhere_ReturnsDiagnostic()
    {
        // Arrange
        var sql = "SELECT * FROM orders WHERE YEAR(order_date) = 2023;";
        var context = RuleTestContext.CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("non-sargable", diagnostic.Code);
        Assert.Contains("YEAR", diagnostic.Message);
    }

    [Fact]
    public void Analyze_SubstringInJoinCondition_ReturnsDiagnostic()
    {
        // Arrange
        var sql = @"
            SELECT *
            FROM users u
            INNER JOIN profiles p ON SUBSTRING(u.username, 1, 5) = p.code;";
        var context = RuleTestContext.CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("non-sargable", diagnostic.Code);
        Assert.Contains("SUBSTRING", diagnostic.Message);
    }

    [Fact]
    public void Analyze_UpperInPredicate_ReturnsDiagnostic()
    {
        // Arrange
        var sql = "SELECT * FROM users WHERE UPPER(username) = 'ADMIN';";
        var context = RuleTestContext.CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("non-sargable", diagnostic.Code);
        Assert.Contains("UPPER", diagnostic.Message);
    }

    [Fact]
    public void Analyze_LowerInPredicate_ReturnsDiagnostic()
    {
        // Arrange
        var sql = "SELECT * FROM users WHERE LOWER(username) = 'admin';";
        var context = RuleTestContext.CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("non-sargable", diagnostic.Code);
        Assert.Contains("LOWER", diagnostic.Message);
    }

    [Fact]
    public void Analyze_CastConvertInPredicate_NoDiagnostic()
    {
        // Arrange - CAST/CONVERT are handled by avoid-implicit-conversion-in-predicate rule
        var sql = "SELECT * FROM users WHERE CAST(user_id AS VARCHAR) = '123';";
        var context = RuleTestContext.CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_FunctionOnLiteral_NoDiagnostic()
    {
        // Arrange
        var sql = "SELECT * FROM users WHERE username = LTRIM('  admin  ');";
        var context = RuleTestContext.CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_FunctionInSelectList_NoDiagnostic()
    {
        // Arrange
        var sql = "SELECT LTRIM(username) FROM users;";
        var context = RuleTestContext.CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_DateAddConstantsInPredicate_NoDiagnostic()
    {
        // Arrange
        const string sql = @"
            SELECT [Name]
            FROM Foo
            WHERE Foo.DateCreated BETWEEN DATEADD(MONTH, DATEDIFF(MONTH, 0, GETDATE()), 0)
              AND DATEADD(DAY, 1, EOMONTH(DATEADD(MONTH, 0, GETDATE())));";
        var context = RuleTestContext.CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_MultipleFunctionsInWhere_ReturnsMultipleDiagnostics()
    {
        // Arrange
        var sql = @"
            SELECT *
            FROM orders
            WHERE YEAR(order_date) = 2023
              AND MONTH(order_date) = 12;";
        var context = RuleTestContext.CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        Assert.Equal(2, diagnostics.Count);
        Assert.All(diagnostics, d => Assert.Equal("non-sargable", d.Code));
    }

    [Fact]
    public void Analyze_FunctionInHavingClause_ReturnsDiagnostic()
    {
        // Arrange
        var sql = @"
            SELECT COUNT(*)
            FROM users
            GROUP BY department
            HAVING LTRIM(department) = 'Sales';";
        var context = RuleTestContext.CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("non-sargable", diagnostic.Code);
    }

    [Fact]
    public void Analyze_NestedFunctionsOnColumn_ReturnsDiagnostic()
    {
        // Arrange - Only the outer function is reported
        var sql = "SELECT * FROM users WHERE LTRIM(RTRIM(username)) = 'admin';";
        var context = RuleTestContext.CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("non-sargable", diagnostic.Code);
        Assert.Contains("LTRIM", diagnostic.Message);
    }

    [Fact]
    public void GetFixes_ReturnsNoFixes()
    {
        // Arrange
        var sql = "SELECT * FROM users WHERE LTRIM(username) = 'admin';";
        var context = RuleTestContext.CreateContext(sql);
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
        Assert.Equal("non-sargable", _rule.Metadata.RuleId);
        Assert.Equal("Performance", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Warning, _rule.Metadata.DefaultSeverity);
        Assert.False(_rule.Metadata.Fixable);
    }
}
