using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Performance;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Performance;

public sealed class PreferExistsOverInSubqueryRuleTests
{
    private readonly PreferExistsOverInSubqueryRule _rule = new();

    [Fact]
    public void Metadata_HasCorrectProperties()
    {
        Assert.Equal("prefer-exists-over-in-subquery", _rule.Metadata.RuleId);
        Assert.Equal("Performance", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Information, _rule.Metadata.DefaultSeverity);
        Assert.False(_rule.Metadata.Fixable);
    }

    [Fact]
    public void Analyze_InWithSubquery_ReturnsDiagnostic()
    {
        const string sql = "SELECT * FROM Users WHERE Id IN (SELECT UserId FROM Orders);";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("prefer-exists-over-in-subquery", diagnostics[0].Code);
        // "IN" starts at column 29, length 2
        Assert.Equal(0, diagnostics[0].Range.Start.Line);
        Assert.Equal(29, diagnostics[0].Range.Start.Character);
        Assert.Equal(0, diagnostics[0].Range.End.Line);
        Assert.Equal(31, diagnostics[0].Range.End.Character);
    }

    [Fact]
    public void Analyze_NotInWithSubquery_ReturnsDiagnostic()
    {
        const string sql = "SELECT * FROM Users WHERE Id NOT IN (SELECT UserId FROM Orders);";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("prefer-exists-over-in-subquery", diagnostics[0].Code);
        // "NOT IN" starts at column 29, ends at column 35
        Assert.Equal(0, diagnostics[0].Range.Start.Line);
        Assert.Equal(29, diagnostics[0].Range.Start.Character);
        Assert.Equal(0, diagnostics[0].Range.End.Line);
        Assert.Equal(35, diagnostics[0].Range.End.Character);
    }

    [Fact]
    public void Analyze_InWithSubqueryInJoinCondition_ReturnsDiagnostic()
    {
        const string sql = @"
            SELECT u.*
            FROM Users u
            INNER JOIN Departments d ON d.Id = u.DeptId
                AND u.Id IN (SELECT UserId FROM ActiveUsers);
        ";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("prefer-exists-over-in-subquery", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_InWithSubqueryInHaving_ReturnsDiagnostic()
    {
        const string sql = @"
            SELECT DeptId, COUNT(*) AS Cnt
            FROM Users
            GROUP BY DeptId
            HAVING DeptId IN (SELECT Id FROM ActiveDepartments);
        ";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("prefer-exists-over-in-subquery", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_MultipleInSubqueries_ReturnsMultipleDiagnostics()
    {
        const string sql = @"
            SELECT * FROM Users
            WHERE Id IN (SELECT UserId FROM Orders)
            AND DeptId IN (SELECT Id FROM ActiveDepartments);
        ";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Equal(2, diagnostics.Length);
        Assert.All(diagnostics, d => Assert.Equal("prefer-exists-over-in-subquery", d.Code));
    }

    [Theory]
    [InlineData("SELECT * FROM Users WHERE Id IN (1, 2, 3);")]
    [InlineData("SELECT * FROM Users WHERE Status IN ('Active', 'Pending');")]
    [InlineData("SELECT * FROM Users WHERE Id IN (@id1, @id2);")]
    public void Analyze_InWithValueList_NoDiagnostic(string sql)
    {
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_ExistsSubquery_NoDiagnostic()
    {
        const string sql = @"
            SELECT * FROM Users u
            WHERE EXISTS (SELECT 1 FROM Orders o WHERE o.UserId = u.Id);
        ";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_InSubqueryInSelectList_NoDiagnostic()
    {
        // IN in SELECT list (not predicate context) should not be flagged
        const string sql = @"
            SELECT
                CASE WHEN Id IN (SELECT UserId FROM Admins) THEN 1 ELSE 0 END AS IsAdmin
            FROM Users;
        ";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Theory]
    [InlineData("SELECT * FROM Users;")]
    [InlineData("")]
    public void Analyze_NoInPredicate_NoDiagnostic(string sql)
    {
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_InWithSubqueryWhereIsNotNullOnSameColumn_NoDiagnostic()
    {
        const string sql = "SELECT * FROM Users WHERE Id IN (SELECT UserId FROM Orders WHERE UserId IS NOT NULL);";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_InWithSubqueryWhereIsNotNullOnDifferentColumn_ReturnsDiagnostic()
    {
        const string sql = "SELECT * FROM Users WHERE Id IN (SELECT UserId FROM Orders WHERE SomeOtherCol IS NOT NULL);";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("prefer-exists-over-in-subquery", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_InWithSubqueryWhereIsNotNullInAndCondition_NoDiagnostic()
    {
        const string sql = "SELECT * FROM Users WHERE Id IN (SELECT UserId FROM Orders WHERE UserId IS NOT NULL AND Status = 'Active');";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_NotInWithSubqueryWhereIsNotNullOnSameColumn_NoDiagnostic()
    {
        const string sql = "SELECT * FROM Users WHERE Id NOT IN (SELECT UserId FROM Orders WHERE UserId IS NOT NULL);";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_InWithSubqueryWhereIsNullOnSameColumn_ReturnsDiagnostic()
    {
        const string sql = "SELECT * FROM Users WHERE Id IN (SELECT UserId FROM Orders WHERE UserId IS NULL);";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("prefer-exists-over-in-subquery", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_InWithSubqueryWhereQualifiedIsNotNullOnSameColumn_NoDiagnostic()
    {
        const string sql = "SELECT * FROM Users WHERE Id IN (SELECT o.UserId FROM Orders o WHERE o.UserId IS NOT NULL);";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_InWithSubqueryWhereIsNotNullOnDifferentQualifier_ReturnsDiagnostic()
    {
        const string sql = """
            SELECT * FROM Users
            WHERE Id IN (
                SELECT o.UserId
                FROM Orders o
                INNER JOIN Blacklist b ON b.UserId = o.UserId
                WHERE b.UserId IS NOT NULL
            );
            """;
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("prefer-exists-over-in-subquery", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_InWithSubqueryWhereParenthesizedIsNotNull_NoDiagnostic()
    {
        const string sql = "SELECT * FROM Users WHERE Id IN (SELECT UserId FROM Orders WHERE (UserId IS NOT NULL) AND Status = 1);";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_InWithSubqueryWhereIsNotNullCaseInsensitive_NoDiagnostic()
    {
        const string sql = "SELECT * FROM Users WHERE Id IN (SELECT userid FROM Orders WHERE USERID IS NOT NULL);";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void GetFixes_ReturnsEmptyCollection()
    {
        const string sql = "SELECT * FROM Users WHERE Id IN (SELECT UserId FROM Orders);";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostic = _rule.Analyze(context).First();

        var fixes = _rule.GetFixes(context, diagnostic);

        Assert.Empty(fixes);
    }
}
