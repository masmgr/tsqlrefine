using TsqlRefine.Rules.Rules.Transactions;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Transactions;

public sealed class TransactionNotClosedOnPathRuleTests
{
    private readonly TransactionNotClosedOnPathRule _rule = new();

    [Theory]
    [InlineData("BEGIN TRANSACTION; RETURN;")]
    [InlineData("BEGIN TRANSACTION; BEGIN TRANSACTION; COMMIT TRANSACTION;")]
    [InlineData("BEGIN TRANSACTION; SAVE TRANSACTION s; ROLLBACK TRANSACTION s;")]
    public void Analyze_TransactionOpenAtExit_ReturnsDiagnostic(string sql)
    {
        Assert.Single(_rule.Analyze(RuleTestContext.CreateContext(sql)));
    }

    [Theory]
    [InlineData("BEGIN TRANSACTION; COMMIT TRANSACTION;")]
    [InlineData("BEGIN TRANSACTION; ROLLBACK TRANSACTION;")]
    [InlineData("COMMIT TRANSACTION;")]
    public void Analyze_TransactionClosedOrCallerOwned_ReturnsNoDiagnostic(string sql)
    {
        Assert.Empty(_rule.Analyze(RuleTestContext.CreateContext(sql)));
    }

    [Fact]
    public void Analyze_OnlyOneBranchCommits_ReturnsDiagnostic()
    {
        const string sql = "BEGIN TRANSACTION; IF @ok = 1 COMMIT TRANSACTION;";

        Assert.Single(_rule.Analyze(RuleTestContext.CreateContext(sql)));
    }

    [Fact]
    public void Analyze_AllBranchesClose_ReturnsNoDiagnostic()
    {
        const string sql = "BEGIN TRANSACTION; IF @ok = 1 COMMIT TRANSACTION; ELSE ROLLBACK TRANSACTION;";

        Assert.Empty(_rule.Analyze(RuleTestContext.CreateContext(sql)));
    }

    [Fact]
    public void Analyze_TryCatchClosesTransaction_ReturnsNoDiagnostic()
    {
        const string sql = """
            BEGIN TRY
                BEGIN TRANSACTION;
                SELECT 1;
                COMMIT TRANSACTION;
            END TRY
            BEGIN CATCH
                ROLLBACK TRANSACTION;
            END CATCH;
            """;

        Assert.Empty(_rule.Analyze(RuleTestContext.CreateContext(sql)));
    }

    [Fact]
    public void Analyze_UnknownAndKnownOpenPaths_ReturnsDiagnostic()
    {
        const string sql = "BEGIN TRANSACTION; IF @run = 1 EXEC(@sql); RETURN;";

        Assert.Single(_rule.Analyze(RuleTestContext.CreateContext(sql)));
    }

    [Theory]
    [InlineData("BEGIN TRANSACTION; IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;")]
    [InlineData("BEGIN TRANSACTION; IF @@TRANCOUNT = 0 RETURN; ELSE ROLLBACK TRANSACTION;")]
    [InlineData("BEGIN TRANSACTION; IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;")]
    [InlineData("BEGIN TRANSACTION; IF 0 < @@TRANCOUNT ROLLBACK TRANSACTION;")]
    public void Analyze_TransactionStateGuardAccountsForClosedBranch_ReturnsNoDiagnostic(string sql)
    {
        Assert.Empty(_rule.Analyze(RuleTestContext.CreateContext(sql)));
    }

    [Fact]
    public void Analyze_ReportsTheBeginThatRemainsOpen()
    {
        const string sql = """
            BEGIN TRANSACTION;
            COMMIT;
            BEGIN TRANSACTION;
            """;

        var diagnostic = Assert.Single(_rule.Analyze(RuleTestContext.CreateContext(sql)));

        Assert.Equal(2, diagnostic.Range.Start.Line);
    }

    [Fact]
    public void Analyze_XactAbortAloneDoesNotSuppressOpenTransaction()
    {
        const string sql = "SET XACT_ABORT ON; BEGIN TRANSACTION; RETURN;";

        Assert.Single(_rule.Analyze(RuleTestContext.CreateContext(sql)));
    }

    [Fact]
    public void Analyze_GotoScope_ReturnsNoDiagnostic()
    {
        const string sql = "BEGIN TRANSACTION; GOTO finish; finish: SELECT 1;";

        Assert.Empty(_rule.Analyze(RuleTestContext.CreateContext(sql)));
    }
}
