using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Style;
using TsqlRefine.Rules.Tests.Helpers;
using Xunit;

namespace TsqlRefine.Rules.Tests.Style;

public sealed class NormalizeProcedureKeywordRuleTests
{
    private readonly NormalizeProcedureKeywordRule _rule = new();

    private static RuleContext CreateContext(string sql)
    {
        return RuleTestContext.CreateContext(sql);
    }

    [Fact]
    public void Analyze_CreateProc_ReturnsDiagnostic()
    {
        var sql = "CREATE PROC TestProc AS SELECT 1;";
        var context = CreateContext(sql);

        var diagnostic = Assert.Single(_rule.Analyze(context));
        Assert.Equal("normalize-procedure-keyword", diagnostic.Code);
        Assert.Equal("Use 'PROCEDURE' instead of 'PROC'.", diagnostic.Message);
    }

    [Fact]
    public void Analyze_AlterProc_ReturnsDiagnostic()
    {
        var sql = "ALTER PROC TestProc AS SELECT 1;";
        var context = CreateContext(sql);

        var diagnostic = Assert.Single(_rule.Analyze(context));
        Assert.Equal("normalize-procedure-keyword", diagnostic.Code);
    }

    [Fact]
    public void Analyze_DropProc_ReturnsDiagnostic()
    {
        var sql = "DROP PROC TestProc;";
        var context = CreateContext(sql);

        var diagnostic = Assert.Single(_rule.Analyze(context));
        Assert.Equal("normalize-procedure-keyword", diagnostic.Code);
    }

    [Fact]
    public void Analyze_CreateOrAlterProc_ReturnsDiagnostic()
    {
        var sql = "CREATE OR ALTER PROC TestProc AS SELECT 1;";
        var context = CreateContext(sql);

        var diagnostic = Assert.Single(_rule.Analyze(context));
        Assert.Equal("normalize-procedure-keyword", diagnostic.Code);
    }

    [Fact]
    public void Analyze_CreateProcedure_NoDiagnostic()
    {
        var sql = "CREATE PROCEDURE TestProc AS SELECT 1;";
        var context = CreateContext(sql);

        Assert.Empty(_rule.Analyze(context));
    }

    [Fact]
    public void Analyze_AlterProcedure_NoDiagnostic()
    {
        var sql = "ALTER PROCEDURE TestProc AS SELECT 1;";
        var context = CreateContext(sql);

        Assert.Empty(_rule.Analyze(context));
    }

    [Fact]
    public void Analyze_DropProcedure_NoDiagnostic()
    {
        var sql = "DROP PROCEDURE TestProc;";
        var context = CreateContext(sql);

        Assert.Empty(_rule.Analyze(context));
    }

    [Fact]
    public void Analyze_LowercaseProc_ReturnsDiagnostic()
    {
        var sql = "create proc TestProc as select 1;";
        var context = CreateContext(sql);

        var diagnostic = Assert.Single(_rule.Analyze(context));
        Assert.Equal("normalize-procedure-keyword", diagnostic.Code);
    }

    [Fact]
    public void Analyze_MultipleProc_ReturnsMultipleDiagnostics()
    {
        var sql = "CREATE PROC Proc1 AS SELECT 1; CREATE PROC Proc2 AS SELECT 2;";
        var context = CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();
        Assert.Equal(2, diagnostics.Length);
    }

    [Fact]
    public void GetFixes_CreateProc_ReturnsProcedure()
    {
        var sql = "CREATE PROC TestProc AS SELECT 1;";
        var context = CreateContext(sql);
        var diagnostic = _rule.Analyze(context).Single();

        var fix = Assert.Single(_rule.GetFixes(context, diagnostic));
        var edit = Assert.Single(fix.Edits);

        Assert.Equal("Use 'PROCEDURE'", fix.Title);
        var updated = Apply(sql, edit);
        Assert.Equal("CREATE PROCEDURE TestProc AS SELECT 1;", updated);
    }

    [Fact]
    public void GetFixes_AlterProc_ReturnsProcedure()
    {
        var sql = "ALTER PROC TestProc AS SELECT 1;";
        var context = CreateContext(sql);
        var diagnostic = _rule.Analyze(context).Single();

        var fix = Assert.Single(_rule.GetFixes(context, diagnostic));
        var edit = Assert.Single(fix.Edits);

        var updated = Apply(sql, edit);
        Assert.Equal("ALTER PROCEDURE TestProc AS SELECT 1;", updated);
    }

    [Fact]
    public void GetFixes_DropProc_ReturnsProcedure()
    {
        var sql = "DROP PROC TestProc;";
        var context = CreateContext(sql);
        var diagnostic = _rule.Analyze(context).Single();

        var fix = Assert.Single(_rule.GetFixes(context, diagnostic));
        var edit = Assert.Single(fix.Edits);

        var updated = Apply(sql, edit);
        Assert.Equal("DROP PROCEDURE TestProc;", updated);
    }

    [Fact]
    public void Metadata_HasCorrectProperties()
    {
        Assert.Equal("normalize-procedure-keyword", _rule.Metadata.RuleId);
        Assert.Equal("Style", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Information, _rule.Metadata.DefaultSeverity);
        Assert.True(_rule.Metadata.Fixable);
    }

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
