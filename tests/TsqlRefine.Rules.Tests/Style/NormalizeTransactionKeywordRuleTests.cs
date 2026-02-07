using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Style;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Style;

public sealed class NormalizeTransactionKeywordRuleTests
{
    private readonly NormalizeTransactionKeywordRule _rule = new();

    private static RuleContext CreateContext(string sql)
    {
        return RuleTestContext.CreateContext(sql);
    }

    #region TRAN Tests

    [Fact]
    public void Analyze_BeginTran_ReturnsDiagnostic()
    {
        var sql = "BEGIN TRAN;";
        var context = CreateContext(sql);

        var diagnostic = Assert.Single(_rule.Analyze(context));
        Assert.Equal("normalize-transaction-keyword", diagnostic.Code);
        Assert.Equal("Use 'TRANSACTION' instead of 'TRAN'.", diagnostic.Message);
    }

    [Fact]
    public void Analyze_CommitTran_ReturnsDiagnostic()
    {
        var sql = "COMMIT TRAN;";
        var context = CreateContext(sql);

        var diagnostic = Assert.Single(_rule.Analyze(context));
        Assert.Equal("normalize-transaction-keyword", diagnostic.Code);
        Assert.Equal("Use 'TRANSACTION' instead of 'TRAN'.", diagnostic.Message);
    }

    [Fact]
    public void Analyze_RollbackTran_ReturnsDiagnostic()
    {
        var sql = "ROLLBACK TRAN;";
        var context = CreateContext(sql);

        var diagnostic = Assert.Single(_rule.Analyze(context));
        Assert.Equal("normalize-transaction-keyword", diagnostic.Code);
        Assert.Equal("Use 'TRANSACTION' instead of 'TRAN'.", diagnostic.Message);
    }

    [Fact]
    public void Analyze_SaveTran_ReturnsDiagnostic()
    {
        var sql = "SAVE TRAN SavePoint1;";
        var context = CreateContext(sql);

        var diagnostic = Assert.Single(_rule.Analyze(context));
        Assert.Equal("normalize-transaction-keyword", diagnostic.Code);
    }

    [Fact]
    public void Analyze_BeginTransaction_NoDiagnostic()
    {
        var sql = "BEGIN TRANSACTION;";
        var context = CreateContext(sql);

        Assert.Empty(_rule.Analyze(context));
    }

    [Fact]
    public void Analyze_CommitTransaction_NoDiagnostic()
    {
        var sql = "COMMIT TRANSACTION;";
        var context = CreateContext(sql);

        Assert.Empty(_rule.Analyze(context));
    }

    [Fact]
    public void Analyze_RollbackTransaction_NoDiagnostic()
    {
        var sql = "ROLLBACK TRANSACTION;";
        var context = CreateContext(sql);

        Assert.Empty(_rule.Analyze(context));
    }

    #endregion

    #region Standalone COMMIT Tests

    [Fact]
    public void Analyze_StandaloneCommit_ReturnsDiagnostic()
    {
        var sql = "COMMIT;";
        var context = CreateContext(sql);

        var diagnostic = Assert.Single(_rule.Analyze(context));
        Assert.Equal("normalize-transaction-keyword", diagnostic.Code);
        Assert.Equal("Use 'COMMIT TRANSACTION' instead of 'COMMIT'.", diagnostic.Message);
    }

    [Fact]
    public void Analyze_CommitWork_NoDiagnostic()
    {
        var sql = "COMMIT WORK;";
        var context = CreateContext(sql);

        Assert.Empty(_rule.Analyze(context));
    }

    [Fact]
    public void Analyze_CommitAtEndOfFile_ReturnsDiagnostic()
    {
        var sql = "COMMIT";
        var context = CreateContext(sql);

        var diagnostic = Assert.Single(_rule.Analyze(context));
        Assert.Equal("Use 'COMMIT TRANSACTION' instead of 'COMMIT'.", diagnostic.Message);
    }

    #endregion

    #region Standalone ROLLBACK Tests

    [Fact]
    public void Analyze_StandaloneRollback_ReturnsDiagnostic()
    {
        var sql = "ROLLBACK;";
        var context = CreateContext(sql);

        var diagnostic = Assert.Single(_rule.Analyze(context));
        Assert.Equal("normalize-transaction-keyword", diagnostic.Code);
        Assert.Equal("Use 'ROLLBACK TRANSACTION' instead of 'ROLLBACK'.", diagnostic.Message);
    }

    [Fact]
    public void Analyze_RollbackWork_NoDiagnostic()
    {
        var sql = "ROLLBACK WORK;";
        var context = CreateContext(sql);

        Assert.Empty(_rule.Analyze(context));
    }

    [Fact]
    public void Analyze_RollbackAtEndOfFile_ReturnsDiagnostic()
    {
        var sql = "ROLLBACK";
        var context = CreateContext(sql);

        var diagnostic = Assert.Single(_rule.Analyze(context));
        Assert.Equal("Use 'ROLLBACK TRANSACTION' instead of 'ROLLBACK'.", diagnostic.Message);
    }

    #endregion

    #region Multiple Violations Tests

    [Fact]
    public void Analyze_MultipleTran_ReturnsMultipleDiagnostics()
    {
        var sql = "BEGIN TRAN; COMMIT TRAN;";
        var context = CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();
        Assert.Equal(2, diagnostics.Length);
    }

    [Fact]
    public void Analyze_MixedViolations_ReturnsAllDiagnostics()
    {
        var sql = "BEGIN TRAN; COMMIT; ROLLBACK;";
        var context = CreateContext(sql);

        // TRAN, standalone COMMIT, standalone ROLLBACK
        // Note: These are in same script but COMMIT and ROLLBACK would never both execute
        var diagnostics = _rule.Analyze(context).ToArray();
        Assert.Equal(3, diagnostics.Length);
    }

    #endregion

    #region Fix Tests - TRAN

    [Fact]
    public void GetFixes_BeginTran_ReturnsTransaction()
    {
        var sql = "BEGIN TRAN;";
        var context = CreateContext(sql);
        var diagnostic = _rule.Analyze(context).Single();

        var fix = Assert.Single(_rule.GetFixes(context, diagnostic));
        var edit = Assert.Single(fix.Edits);

        Assert.Equal("Use 'TRANSACTION'", fix.Title);
        var updated = Apply(sql, edit);
        Assert.Equal("BEGIN TRANSACTION;", updated);
    }

    [Fact]
    public void GetFixes_CommitTran_ReturnsTransaction()
    {
        var sql = "COMMIT TRAN;";
        var context = CreateContext(sql);
        var diagnostic = _rule.Analyze(context).Single();

        var fix = Assert.Single(_rule.GetFixes(context, diagnostic));
        var edit = Assert.Single(fix.Edits);

        var updated = Apply(sql, edit);
        Assert.Equal("COMMIT TRANSACTION;", updated);
    }

    [Fact]
    public void GetFixes_RollbackTran_ReturnsTransaction()
    {
        var sql = "ROLLBACK TRAN;";
        var context = CreateContext(sql);
        var diagnostic = _rule.Analyze(context).Single();

        var fix = Assert.Single(_rule.GetFixes(context, diagnostic));
        var edit = Assert.Single(fix.Edits);

        var updated = Apply(sql, edit);
        Assert.Equal("ROLLBACK TRANSACTION;", updated);
    }

    #endregion

    #region Fix Tests - Standalone COMMIT/ROLLBACK

    [Fact]
    public void GetFixes_StandaloneCommit_InsertsTransaction()
    {
        var sql = "COMMIT;";
        var context = CreateContext(sql);
        var diagnostic = _rule.Analyze(context).Single();

        var fix = Assert.Single(_rule.GetFixes(context, diagnostic));
        var edit = Assert.Single(fix.Edits);

        Assert.Equal("Use 'COMMIT TRANSACTION'", fix.Title);
        var updated = Apply(sql, edit);
        Assert.Equal("COMMIT TRANSACTION;", updated);
    }

    [Fact]
    public void GetFixes_StandaloneRollback_InsertsTransaction()
    {
        var sql = "ROLLBACK;";
        var context = CreateContext(sql);
        var diagnostic = _rule.Analyze(context).Single();

        var fix = Assert.Single(_rule.GetFixes(context, diagnostic));
        var edit = Assert.Single(fix.Edits);

        Assert.Equal("Use 'ROLLBACK TRANSACTION'", fix.Title);
        var updated = Apply(sql, edit);
        Assert.Equal("ROLLBACK TRANSACTION;", updated);
    }

    #endregion

    #region Metadata Tests

    [Fact]
    public void Metadata_HasCorrectProperties()
    {
        Assert.Equal("normalize-transaction-keyword", _rule.Metadata.RuleId);
        Assert.Equal("Style", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Information, _rule.Metadata.DefaultSeverity);
        Assert.True(_rule.Metadata.Fixable);
    }

    #endregion

    private static string Apply(string text, TextEdit edit)
    {
        var startIndex = IndexFromPosition(text, edit.Range.Start);
        var endIndex = IndexFromPosition(text, edit.Range.End);
        return string.Concat(text.AsSpan(0, startIndex), edit.NewText, text.AsSpan(endIndex));
    }

    private static int IndexFromPosition(string text, Position position)
    {
        var line = 0;
        var character = 0;

        for (var i = 0; i < text.Length; i++)
        {
            if (line == position.Line && character == position.Character)
            {
                return i;
            }

            var ch = text[i];
            if (ch == '\r')
            {
                if (i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }

                line++;
                character = 0;
                continue;
            }

            if (ch == '\n')
            {
                line++;
                character = 0;
                continue;
            }

            character++;
        }

        if (line == position.Line && character == position.Character)
        {
            return text.Length;
        }

        throw new ArgumentOutOfRangeException(nameof(position), $"Position {position.Line}:{position.Character} is outside the text.");
    }
}
