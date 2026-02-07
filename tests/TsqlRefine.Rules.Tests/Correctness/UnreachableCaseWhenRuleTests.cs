using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Correctness;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Correctness;

public sealed class UnreachableCaseWhenRuleTests
{
    private readonly UnreachableCaseWhenRule _rule = new();

    [Fact]
    public void Metadata_HasCorrectProperties()
    {
        Assert.Equal("unreachable-case-when", _rule.Metadata.RuleId);
        Assert.Equal("Correctness", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Warning, _rule.Metadata.DefaultSeverity);
        Assert.False(_rule.Metadata.Fixable);
    }

    [Fact]
    public void Analyze_SimpleCaseDuplicateWhen_ReturnsDiagnostic()
    {
        const string sql = "SELECT CASE @x WHEN 1 THEN 'a' WHEN 1 THEN 'b' END;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("unreachable-case-when", diagnostics[0].Code);
        Assert.Contains("Duplicate", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_SearchedCaseDuplicateWhen_ReturnsDiagnostic()
    {
        const string sql = "SELECT CASE WHEN @x = 1 THEN 'a' WHEN @x = 1 THEN 'b' END;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("unreachable-case-when", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_SimpleCaseMultipleDuplicates_ReturnsMultipleDiagnostics()
    {
        const string sql = "SELECT CASE @x WHEN 1 THEN 'a' WHEN 2 THEN 'b' WHEN 1 THEN 'c' WHEN 2 THEN 'd' END;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Equal(2, diagnostics.Length);
        Assert.All(diagnostics, d => Assert.Equal("unreachable-case-when", d.Code));
    }

    [Fact]
    public void Analyze_SimpleCaseStringDuplicates_ReturnsDiagnostic()
    {
        const string sql = "SELECT CASE @status WHEN 'Active' THEN 1 WHEN 'Active' THEN 2 END;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("unreachable-case-when", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_SearchedCaseDuplicateComplex_ReturnsDiagnostic()
    {
        const string sql = @"
            SELECT CASE
                WHEN @x > 10 AND @y < 5 THEN 'a'
                WHEN @x > 10 AND @y < 5 THEN 'b'
            END;
        ";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("unreachable-case-when", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_SimpleCaseDistinctValues_NoDiagnostic()
    {
        const string sql = "SELECT CASE @x WHEN 1 THEN 'a' WHEN 2 THEN 'b' WHEN 3 THEN 'c' END;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_SearchedCaseDistinctConditions_NoDiagnostic()
    {
        const string sql = "SELECT CASE WHEN @x = 1 THEN 'a' WHEN @x = 2 THEN 'b' END;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_SimpleCaseWithElse_NoDiagnostic()
    {
        const string sql = "SELECT CASE @x WHEN 1 THEN 'a' ELSE 'b' END;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_NestedCaseExpressions_FlagsDuplicatesInEach()
    {
        const string sql = @"
            SELECT
                CASE @x WHEN 1 THEN 'a' WHEN 1 THEN 'b' END,
                CASE @y WHEN 'A' THEN 1 WHEN 'A' THEN 2 END;
        ";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Equal(2, diagnostics.Length);
        Assert.All(diagnostics, d => Assert.Equal("unreachable-case-when", d.Code));
    }

    [Fact]
    public void Analyze_CaseInsensitiveComparison_ReturnsDiagnostic()
    {
        // WHEN conditions should be compared case-insensitively for identifiers
        const string sql = "SELECT CASE WHEN @X = 1 THEN 'a' WHEN @x = 1 THEN 'b' END;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("unreachable-case-when", diagnostics[0].Code);
    }

    [Theory]
    [InlineData("SELECT * FROM Users;")]
    [InlineData("")]
    public void Analyze_NoCaseExpression_NoDiagnostic(string sql)
    {
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void GetFixes_ReturnsEmptyCollection()
    {
        const string sql = "SELECT CASE @x WHEN 1 THEN 'a' WHEN 1 THEN 'b' END;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostic = _rule.Analyze(context).First();

        var fixes = _rule.GetFixes(context, diagnostic);

        Assert.Empty(fixes);
    }
}
