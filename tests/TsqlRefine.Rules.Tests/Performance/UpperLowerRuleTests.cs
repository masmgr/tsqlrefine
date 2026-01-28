using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules;
using TsqlRefine.Rules.Rules.Performance;
using Xunit;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Performance;

public sealed class UpperLowerRuleTests
{
    private readonly UpperLowerRule _rule = new();



    [Fact]
    public void Analyze_UpperInWhereClause_ReturnsDiagnostic()
    {
        // Arrange
        var sql = "SELECT * FROM users WHERE UPPER(username) = 'ADMIN';";
        var context = RuleTestContext.CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("upper-lower", diagnostic.Code);
        Assert.Contains("UPPER", diagnostic.Message);
        Assert.Contains("index", diagnostic.Message.ToLowerInvariant());
    }

    [Fact]
    public void Analyze_LowerInWhereClause_ReturnsDiagnostic()
    {
        // Arrange
        var sql = "SELECT * FROM users WHERE LOWER(email) = 'test@example.com';";
        var context = RuleTestContext.CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("upper-lower", diagnostic.Code);
        Assert.Contains("LOWER", diagnostic.Message);
    }

    [Fact]
    public void Analyze_UpperInJoinCondition_ReturnsDiagnostic()
    {
        // Arrange
        var sql = @"
            SELECT *
            FROM users u
            INNER JOIN profiles p ON UPPER(u.username) = UPPER(p.username);";
        var context = RuleTestContext.CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        Assert.Equal(2, diagnostics.Count);
        Assert.All(diagnostics, d => Assert.Equal("upper-lower", d.Code));
    }

    [Fact]
    public void Analyze_UpperOnLiteral_NoDiagnostic()
    {
        // Arrange - UPPER on literal is acceptable
        var sql = "SELECT * FROM users WHERE username = UPPER('admin');";
        var context = RuleTestContext.CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_UpperInSelectList_NoDiagnostic()
    {
        // Arrange - UPPER in SELECT list is acceptable
        var sql = "SELECT UPPER(username) FROM users;";
        var context = RuleTestContext.CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_UpperWithDateAddConstants_NoDiagnostic()
    {
        // Arrange
        const string sql = @"
            SELECT *
            FROM Foo
            WHERE UPPER(DATEADD(MONTH, DATEDIFF(MONTH, 0, GETDATE()), 0)) = 'CONSTANT';";
        var context = RuleTestContext.CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_LowerInHavingClause_ReturnsDiagnostic()
    {
        // Arrange
        var sql = @"
            SELECT COUNT(*)
            FROM users
            GROUP BY department
            HAVING LOWER(department) = 'sales';";
        var context = RuleTestContext.CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("upper-lower", diagnostic.Code);
    }

    [Fact]
    public void Analyze_NoUpperLower_NoDiagnostic()
    {
        // Arrange
        var sql = "SELECT * FROM users WHERE username = 'admin';";
        var context = RuleTestContext.CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_UpperLowerBothSides_ReturnsDiagnostics()
    {
        // Arrange
        var sql = "SELECT * FROM users WHERE UPPER(username) = UPPER(email);";
        var context = RuleTestContext.CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        Assert.Equal(2, diagnostics.Count);
        Assert.All(diagnostics, d => Assert.Equal("upper-lower", d.Code));
    }

    [Fact]
    public void GetFixes_ReturnsNoFixes()
    {
        // Arrange
        var sql = "SELECT * FROM users WHERE UPPER(username) = 'ADMIN';";
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
        Assert.Equal("upper-lower", _rule.Metadata.RuleId);
        Assert.Equal("Performance", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Warning, _rule.Metadata.DefaultSeverity);
        Assert.False(_rule.Metadata.Fixable);
    }
}
