using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Transactions;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Transactions;

public sealed class RequireSaveTransactionInNestedRuleTests
{
    private readonly RequireSaveTransactionInNestedRule _rule = new();

    [Fact]
    public void Metadata_HasCorrectProperties()
    {
        Assert.Equal("require-save-transaction-in-nested", _rule.Metadata.RuleId);
        Assert.Equal("Transactions", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Information, _rule.Metadata.DefaultSeverity);
        Assert.False(_rule.Metadata.Fixable);
    }

    [Fact]
    public void Analyze_NestedTransactionWithoutSave_ReturnsDiagnostic()
    {
        const string sql = @"
            BEGIN TRANSACTION;
                BEGIN TRANSACTION;
                    SELECT 1;
                COMMIT;
            COMMIT;
        ";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("require-save-transaction-in-nested", diagnostics[0].Code);
        Assert.Contains("Nested", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_NestedTransactionWithSave_NoDiagnostic()
    {
        const string sql = @"
            BEGIN TRANSACTION;
                SAVE TRANSACTION SavePoint1;
                BEGIN TRANSACTION;
                    SELECT 1;
                COMMIT;
            COMMIT;
        ";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_SingleTransaction_NoDiagnostic()
    {
        const string sql = @"
            BEGIN TRANSACTION;
                SELECT 1;
            COMMIT;
        ";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_TripleNestedWithoutSave_ReturnsDiagnostics()
    {
        const string sql = @"
            BEGIN TRANSACTION;
                BEGIN TRANSACTION;
                    BEGIN TRANSACTION;
                        SELECT 1;
                    COMMIT;
                COMMIT;
            COMMIT;
        ";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        // Both the second and third BEGIN TRANSACTION should be flagged
        Assert.Equal(2, diagnostics.Length);
        Assert.All(diagnostics, d => Assert.Equal("require-save-transaction-in-nested", d.Code));
    }

    [Fact]
    public void Analyze_NestedTransactionWithRollback_ReturnsDiagnostic()
    {
        const string sql = @"
            BEGIN TRANSACTION;
                BEGIN TRANSACTION;
                    SELECT 1;
                ROLLBACK;
            ROLLBACK;
        ";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("require-save-transaction-in-nested", diagnostics[0].Code);
    }

    [Theory]
    [InlineData("SELECT * FROM Users;")]
    [InlineData("")]
    public void Analyze_NoTransaction_NoDiagnostic(string sql)
    {
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_SaveTransactionAlone_NoDiagnostic()
    {
        const string sql = @"
            BEGIN TRANSACTION;
                SAVE TRANSACTION sp1;
                SELECT 1;
            COMMIT;
        ";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void GetFixes_ReturnsEmptyCollection()
    {
        const string sql = @"
            BEGIN TRANSACTION;
                BEGIN TRANSACTION;
                    SELECT 1;
                COMMIT;
            COMMIT;
        ";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostic = _rule.Analyze(context).First();

        var fixes = _rule.GetFixes(context, diagnostic);

        Assert.Empty(fixes);
    }
}
