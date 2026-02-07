using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Performance;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Performance;

public sealed class LikeLeadingWildcardRuleTests
{
    private readonly LikeLeadingWildcardRule _rule = new();

    [Fact]
    public void Metadata_HasCorrectProperties()
    {
        Assert.Equal("like-leading-wildcard", _rule.Metadata.RuleId);
        Assert.Equal("Performance", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Warning, _rule.Metadata.DefaultSeverity);
        Assert.False(_rule.Metadata.Fixable);
    }

    [Fact]
    public void Analyze_LeadingPercent_ReturnsDiagnostic()
    {
        const string sql = "SELECT * FROM dbo.Users WHERE Name LIKE '%smith';";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("like-leading-wildcard", diagnostics[0].Code);
        Assert.Contains("leading wildcard", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_LeadingPercentWithTrailingPercent_ReturnsDiagnostic()
    {
        const string sql = "SELECT * FROM dbo.Users WHERE Name LIKE '%smith%';";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("like-leading-wildcard", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_LeadingUnderscore_ReturnsDiagnostic()
    {
        const string sql = "SELECT * FROM dbo.Users WHERE Code LIKE '_BC';";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("like-leading-wildcard", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_LeadingBracketWildcard_ReturnsDiagnostic()
    {
        const string sql = "SELECT * FROM dbo.Users WHERE Code LIKE '[A-Z]BC';";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("like-leading-wildcard", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_InJoinCondition_ReturnsDiagnostic()
    {
        const string sql = @"
            SELECT *
            FROM dbo.Users AS u
            INNER JOIN dbo.Profiles AS p ON u.Name LIKE '%test';
        ";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("like-leading-wildcard", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_NotLikeWithLeadingWildcard_ReturnsDiagnostic()
    {
        const string sql = "SELECT * FROM dbo.Users WHERE Name NOT LIKE '%test';";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("like-leading-wildcard", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_MultipleLeadingWildcards_ReturnsMultipleDiagnostics()
    {
        const string sql = @"
            SELECT * FROM dbo.Users
            WHERE Name LIKE '%smith'
              AND Email LIKE '%@gmail.com';
        ";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Equal(2, diagnostics.Length);
        Assert.All(diagnostics, d => Assert.Equal("like-leading-wildcard", d.Code));
    }

    [Theory]
    [InlineData("SELECT * FROM dbo.Users WHERE Name LIKE 'smith%';")]
    [InlineData("SELECT * FROM dbo.Users WHERE Name LIKE 'smith';")]
    [InlineData("SELECT * FROM dbo.Users WHERE Name LIKE 'sm%th';")]
    [InlineData("SELECT * FROM dbo.Users WHERE Name = 'test';")]
    [InlineData("SELECT * FROM dbo.Users;")]
    [InlineData("")]
    public void Analyze_NoLeadingWildcard_NoDiagnostic(string sql)
    {
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_LeadingWildcardInSelect_NoDiagnostic()
    {
        // LIKE in SELECT list (not a predicate) should not be flagged
        const string sql = @"
            SELECT CASE WHEN Name LIKE '%smith' THEN 1 ELSE 0 END AS IsSmith
            FROM dbo.Users;
        ";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_VariablePattern_NoDiagnostic()
    {
        // Variable patterns can't be statically analyzed
        const string sql = "SELECT * FROM dbo.Users WHERE Name LIKE @pattern;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void GetFixes_ReturnsEmptyCollection()
    {
        const string sql = "SELECT * FROM dbo.Users WHERE Name LIKE '%smith';";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostic = _rule.Analyze(context).First();

        var fixes = _rule.GetFixes(context, diagnostic);

        Assert.Empty(fixes);
    }
}
