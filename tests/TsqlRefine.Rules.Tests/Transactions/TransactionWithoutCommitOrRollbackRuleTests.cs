using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Transactions;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Transactions;

public sealed class TransactionWithoutCommitOrRollbackRuleTests
{
    private readonly TransactionWithoutCommitOrRollbackRule _rule = new();

    [Fact]
    public void Metadata_HasCorrectProperties()
    {
        Assert.Equal("avoid-transaction-without-commit", _rule.Metadata.RuleId);
        Assert.Equal("Transactions", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Error, _rule.Metadata.DefaultSeverity);
        Assert.False(_rule.Metadata.Fixable);
    }

    [Fact]
    public void Analyze_BeginWithoutCommitInBatch_ReturnsDiagnostic()
    {
        const string sql = @"
BEGIN TRANSACTION;
SELECT 1;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("avoid-transaction-without-commit", diagnostics[0].Code);
        // Diagnostic should highlight only "BEGIN TRANSACTION" keywords
        Assert.Equal(1, diagnostics[0].Range.Start.Line);
        Assert.Equal(0, diagnostics[0].Range.Start.Character);
        Assert.Equal(1, diagnostics[0].Range.End.Line);
        Assert.Equal(17, diagnostics[0].Range.End.Character);
    }

    [Fact]
    public void Analyze_BeginWithCommit_NoDiagnostic()
    {
        const string sql = @"
BEGIN TRANSACTION;
SELECT 1;
COMMIT;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_BeginWithRollback_NoDiagnostic()
    {
        const string sql = @"
BEGIN TRANSACTION;
SELECT 1;
ROLLBACK;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_TwoBeginsWithOneCommit_ReturnsOneDiagnostic()
    {
        const string sql = "BEGIN TRANSACTION; BEGIN TRANSACTION; COMMIT;";

        var diagnostics = _rule.Analyze(RuleTestContext.CreateContext(sql)).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal(0, diagnostics[0].Range.Start.Character);
    }

    [Fact]
    public void Analyze_RollbackToSavepoint_DoesNotCloseTransaction()
    {
        const string sql = "BEGIN TRANSACTION; SAVE TRANSACTION s; ROLLBACK TRANSACTION s;";

        var diagnostics = _rule.Analyze(RuleTestContext.CreateContext(sql)).ToArray();

        Assert.Single(diagnostics);
    }

    [Fact]
    public void Analyze_UnnamedRollbackClosesAllNestedTransactions()
    {
        const string sql = "BEGIN TRANSACTION; BEGIN TRANSACTION; ROLLBACK TRANSACTION;";

        var diagnostics = _rule.Analyze(RuleTestContext.CreateContext(sql)).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_RollbackToNamedTransaction_ClosesTransaction()
    {
        const string sql = "BEGIN TRANSACTION outer_transaction; ROLLBACK TRANSACTION outer_transaction;";

        var diagnostics = _rule.Analyze(RuleTestContext.CreateContext(sql)).ToArray();

        Assert.Empty(diagnostics);
    }

    [Theory]
    [InlineData("SELECT * FROM dbo.Users;")]
    [InlineData("")]
    public void Analyze_NoTransaction_NoDiagnostic(string sql)
    {
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void GetFixes_ReturnsEmptyCollection()
    {
        const string sql = @"
BEGIN TRANSACTION;
SELECT 1;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostic = _rule.Analyze(context).First();

        var fixes = _rule.GetFixes(context, diagnostic);

        Assert.Empty(fixes);
    }
}
