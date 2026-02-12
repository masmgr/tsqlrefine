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
        Assert.Equal("transaction-without-commit-or-rollback", _rule.Metadata.RuleId);
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
        Assert.Equal("transaction-without-commit-or-rollback", diagnostics[0].Code);
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
