using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Transactions;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Transactions;

public sealed class CatchSwallowingRuleTests
{
    private readonly CatchSwallowingRule _rule = new();

    [Fact]
    public void Metadata_HasCorrectProperties()
    {
        Assert.Equal("avoid-catch-swallowing", _rule.Metadata.RuleId);
        Assert.Equal("Transactions", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Warning, _rule.Metadata.DefaultSeverity);
        Assert.False(_rule.Metadata.Fixable);
    }

    [Fact]
    public void Analyze_CatchWithoutPropagation_ReturnsDiagnostic()
    {
        const string sql = @"
BEGIN TRY
    SELECT 1;
END TRY
BEGIN CATCH
    PRINT 'Error';
END CATCH;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("avoid-catch-swallowing", diagnostics[0].Code);
        // Diagnostic should highlight only "BEGIN CATCH" keywords
        Assert.Equal(4, diagnostics[0].Range.Start.Line);
        Assert.Equal(0, diagnostics[0].Range.Start.Character);
        Assert.Equal(4, diagnostics[0].Range.End.Line);
        Assert.Equal(11, diagnostics[0].Range.End.Character);
    }

    [Fact]
    public void Analyze_EmptyCatch_ReturnsDiagnostic()
    {
        const string sql = @"
BEGIN TRY
    SELECT 1;
END TRY
BEGIN CATCH
END CATCH;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("avoid-catch-swallowing", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_CatchWithThrow_NoDiagnostic()
    {
        const string sql = @"
BEGIN TRY
    SELECT 1;
END TRY
BEGIN CATCH
    THROW;
END CATCH;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_CatchWithRaiserror_NoDiagnostic()
    {
        const string sql = @"
BEGIN TRY
    SELECT 1;
END TRY
BEGIN CATCH
    DECLARE @msg NVARCHAR(4000) = ERROR_MESSAGE();
    RAISERROR(@msg, 16, 1);
END CATCH;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Theory]
    [InlineData("SELECT * FROM dbo.Users;")]
    [InlineData("")]
    public void Analyze_NoTryCatch_NoDiagnostic(string sql)
    {
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void GetFixes_ReturnsEmptyCollection()
    {
        const string sql = @"
BEGIN TRY
    SELECT 1;
END TRY
BEGIN CATCH
    PRINT 'Error';
END CATCH;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostic = _rule.Analyze(context).First();

        var fixes = _rule.GetFixes(context, diagnostic);

        Assert.Empty(fixes);
    }
}
