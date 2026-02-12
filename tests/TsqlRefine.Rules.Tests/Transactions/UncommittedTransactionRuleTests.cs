using TsqlRefine.Rules.Rules.Transactions;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Transactions;

public sealed class UncommittedTransactionRuleTests
{
    private readonly UncommittedTransactionRule _rule = new();

    [Fact]
    public void Analyze_BeginWithoutCommit_ReportsDiagnostic()
    {
        var sql = @"
BEGIN TRANSACTION;
UPDATE Users SET Name = 'test' WHERE Id = 1;
";

        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("uncommitted-transaction", diagnostics[0].Data?.RuleId);
        Assert.Contains("BEGIN TRANSACTION without corresponding COMMIT TRANSACTION", diagnostics[0].Message);
        // Diagnostic should highlight only "BEGIN TRANSACTION" keywords
        Assert.Equal(1, diagnostics[0].Range.Start.Line);
        Assert.Equal(0, diagnostics[0].Range.Start.Character);
        Assert.Equal(1, diagnostics[0].Range.End.Line);
        Assert.Equal(17, diagnostics[0].Range.End.Character);
    }

    [Fact]
    public void Analyze_BeginWithCommit_NoDiagnostic()
    {
        var sql = @"
BEGIN TRANSACTION;
UPDATE Users SET Name = 'test' WHERE Id = 1;
COMMIT TRANSACTION;
";

        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_BeginWithRollback_NoDiagnostic()
    {
        var sql = @"
BEGIN TRANSACTION;
UPDATE Users SET Name = 'test' WHERE Id = 1;
ROLLBACK TRANSACTION;
";

        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_MultipleBeginOneCommit_ReportsOnlyUncommitted()
    {
        var sql = @"
BEGIN TRANSACTION;
UPDATE Users SET Name = 'test1' WHERE Id = 1;

BEGIN TRANSACTION;
UPDATE Users SET Name = 'test2' WHERE Id = 2;
COMMIT TRANSACTION;
";

        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        // Only the first BEGIN should be reported
        Assert.Single(diagnostics);
        Assert.Equal("uncommitted-transaction", diagnostics[0].Data?.RuleId);
    }

    [Fact]
    public void Analyze_BeginTranShorthand_WithCommit_NoDiagnostic()
    {
        var sql = @"
BEGIN TRAN;
UPDATE Users SET Name = 'test' WHERE Id = 1;
COMMIT TRAN;
";

        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_NestedTransactions_WithCommits_NoDiagnostic()
    {
        var sql = @"
BEGIN TRANSACTION;
    UPDATE Users SET Name = 'test1' WHERE Id = 1;

    BEGIN TRANSACTION;
        UPDATE Users SET Name = 'test2' WHERE Id = 2;
    COMMIT TRANSACTION;

COMMIT TRANSACTION;
";

        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_BeginInTryCatchWithCommit_NoDiagnostic()
    {
        var sql = @"
BEGIN TRY
    BEGIN TRANSACTION;
    UPDATE Users SET Name = 'test' WHERE Id = 1;
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION;
END CATCH
";

        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_BeginInTryCatchWithoutCommit_ReportsDiagnostic()
    {
        var sql = @"
BEGIN TRY
    BEGIN TRANSACTION;
    UPDATE Users SET Name = 'test' WHERE Id = 1;
END TRY
BEGIN CATCH
    PRINT 'Error occurred';
END CATCH
";

        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("uncommitted-transaction", diagnostics[0].Data?.RuleId);
    }

    [Fact]
    public void Analyze_EmptyScript_NoDiagnostic()
    {
        var sql = "";

        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_OnlyCommit_NoDiagnostic()
    {
        var sql = @"
COMMIT TRANSACTION;
";

        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void GetFixes_ReturnsEmpty()
    {
        var sql = @"
BEGIN TRANSACTION;
UPDATE Users SET Name = 'test' WHERE Id = 1;
";

        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();
        var fixes = _rule.GetFixes(context, diagnostics[0]);

        Assert.Empty(fixes);
    }
}
