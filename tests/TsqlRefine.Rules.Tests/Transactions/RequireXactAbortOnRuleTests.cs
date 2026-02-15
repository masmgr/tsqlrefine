using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Transactions;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Transactions;

public sealed class RequireXactAbortOnRuleTests
{
    private readonly RequireXactAbortOnRule _rule = new();

    [Theory]
    [InlineData("BEGIN TRANSACTION; UPDATE users SET active = 1; COMMIT;")]
    [InlineData(@"BEGIN TRANSACTION;
                    DELETE FROM logs WHERE date < '2023-01-01';
                  COMMIT TRANSACTION;")]
    public void Analyze_TransactionWithoutXactAbortOn_ReturnsDiagnostic(string sql)
    {
        // Arrange
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
        Assert.Equal("set-xact-abort", diagnostics[0].Code);
        Assert.Contains("XACT_ABORT", diagnostics[0].Message);
    }

    [Theory]
    [InlineData(@"SET XACT_ABORT ON;
                  BEGIN TRANSACTION;
                  UPDATE users SET active = 1;
                  COMMIT;")]
    [InlineData(@"SET XACT_ABORT ON
                  GO
                  BEGIN TRANSACTION;
                  DELETE FROM logs;
                  COMMIT TRANSACTION;")]
    public void Analyze_TransactionWithXactAbortOn_ReturnsEmpty(string sql)
    {
        // Arrange
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_XactAbortOffBeforeTransaction_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = @"
            SET XACT_ABORT OFF;
            BEGIN TRANSACTION;
            UPDATE data SET value = 1;
            COMMIT;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
        Assert.Equal("set-xact-abort", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_MultipleTransactionsWithOneXactAbort_AllCovered()
    {
        // Arrange - SET XACT_ABORT ON persists for all subsequent transactions
        const string sql = @"
            SET XACT_ABORT ON;
            BEGIN TRANSACTION;
            UPDATE users SET name = 'test';
            COMMIT;

            -- Second transaction is also covered by earlier SET XACT_ABORT ON
            BEGIN TRANSACTION;
            DELETE FROM logs;
            COMMIT;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert - SET XACT_ABORT ON flag persists, so both transactions are OK
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_XactAbortOnAfterTransaction_ReturnsDiagnostic()
    {
        // Arrange - SET XACT_ABORT ON comes after BEGIN TRANSACTION
        const string sql = @"
            BEGIN TRANSACTION;
            SET XACT_ABORT ON;
            UPDATE data SET value = 1;
            COMMIT;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
    }

    [Fact]
    public void Analyze_NestedTransactions_ChecksEachOne()
    {
        // Arrange
        const string sql = @"
            SET XACT_ABORT ON;
            BEGIN TRANSACTION outer_tran;
                BEGIN TRANSACTION inner_tran;
                UPDATE data SET value = 1;
                COMMIT TRANSACTION inner_tran;
            COMMIT TRANSACTION outer_tran;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert - SET XACT_ABORT ON covers all nested transactions
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_NoTransactions_ReturnsEmpty()
    {
        // Arrange
        const string sql = @"
            SELECT * FROM users;
            UPDATE users SET active = 1 WHERE id = 123;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_ImplicitTransactionContext_NoExplicitBeginTran_ReturnsEmpty()
    {
        // Arrange
        const string sql = @"
            SET XACT_ABORT ON;
            UPDATE users SET active = 1;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert - Rule only checks explicit BEGIN TRANSACTION
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_EmptyInput_ReturnsEmpty()
    {
        // Arrange
        var context = CreateContext("");

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Metadata_HasCorrectProperties()
    {
        // Assert
        Assert.Equal("set-xact-abort", _rule.Metadata.RuleId);
        Assert.Equal("Transactions", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Warning, _rule.Metadata.DefaultSeverity);
        Assert.False(_rule.Metadata.Fixable);
        Assert.Contains("XACT_ABORT", _rule.Metadata.Description);
    }

    [Fact]
    public void GetFixes_ReturnsEmpty()
    {
        // Arrange
        var context = CreateContext("BEGIN TRANSACTION; UPDATE users SET active = 1; COMMIT;");
        var diagnostic = new Diagnostic(
            Range: new TsqlRefine.PluginSdk.Range(new Position(0, 0), new Position(0, 10)),
            Message: "test",
            Code: "set-xact-abort"
        );

        // Act
        var fixes = _rule.GetFixes(context, diagnostic).ToArray();

        // Assert
        Assert.Empty(fixes);
    }

    private static RuleContext CreateContext(string sql, int compatLevel = 150)
    {
        return RuleTestContext.CreateContext(sql, compatLevel);
    }
}
