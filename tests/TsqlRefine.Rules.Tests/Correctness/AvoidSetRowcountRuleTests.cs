using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Correctness;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Correctness;

public sealed class AvoidSetRowcountRuleTests
{
    private readonly AvoidSetRowcountRule _rule = new();

    [Fact]
    public void Metadata_HasCorrectProperties()
    {
        Assert.Equal("avoid-set-rowcount", _rule.Metadata.RuleId);
        Assert.Equal("Correctness", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Warning, _rule.Metadata.DefaultSeverity);
        Assert.False(_rule.Metadata.Fixable);
    }

    [Fact]
    public void Analyze_SetRowcountPositiveInteger_ReturnsDiagnostic()
    {
        const string sql = "SET ROWCOUNT 100;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("avoid-set-rowcount", diagnostics[0].Code);
        Assert.Contains("SET ROWCOUNT", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_SetRowcountVariable_ReturnsDiagnostic()
    {
        const string sql = @"
            DECLARE @rows INT = 50;
            SET ROWCOUNT @rows;
        ";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("avoid-set-rowcount", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_SetRowcountOne_ReturnsDiagnostic()
    {
        const string sql = "SET ROWCOUNT 1;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("avoid-set-rowcount", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_SetRowcountInProcedure_ReturnsDiagnostic()
    {
        const string sql = @"
            CREATE PROCEDURE dbo.LimitResults
            AS
            BEGIN
                SET ROWCOUNT 10;
                SELECT * FROM dbo.Products;
            END;
        ";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("avoid-set-rowcount", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_MultipleSetRowcount_ReturnsMultipleDiagnostics()
    {
        const string sql = @"
            SET ROWCOUNT 100;
            SELECT * FROM dbo.Users;
            SET ROWCOUNT 50;
            DELETE FROM dbo.OldRecords WHERE Status = 'Archived';
        ";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Equal(2, diagnostics.Length);
        Assert.All(diagnostics, d => Assert.Equal("avoid-set-rowcount", d.Code));
    }

    [Fact]
    public void Analyze_SetRowcountZero_NoDiagnostic()
    {
        const string sql = "SET ROWCOUNT 0;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Theory]
    [InlineData("SELECT TOP 100 * FROM dbo.Users;")]
    [InlineData("DELETE TOP (1000) FROM dbo.OldRecords WHERE Status = 'Archived';")]
    [InlineData("SELECT * FROM dbo.Users WHERE Active = 1;")]
    [InlineData("SET NOCOUNT ON;")]
    [InlineData("")]
    public void Analyze_NoSetRowcount_NoDiagnostic(string sql)
    {
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_SetRowcountFollowedByReset_ReturnsOneDiagnostic()
    {
        const string sql = @"
            SET ROWCOUNT 100;
            SELECT * FROM dbo.Users;
            SET ROWCOUNT 0;
        ";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("avoid-set-rowcount", diagnostics[0].Code);
    }

    [Fact]
    public void GetFixes_ReturnsEmptyCollection()
    {
        const string sql = "SET ROWCOUNT 100;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostic = _rule.Analyze(context).First();

        var fixes = _rule.GetFixes(context, diagnostic);

        Assert.Empty(fixes);
    }
}
