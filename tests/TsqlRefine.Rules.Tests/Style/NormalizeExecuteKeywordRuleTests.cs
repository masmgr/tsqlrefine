using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Style;
using TsqlRefine.Rules.Tests.Helpers;
using Xunit;

namespace TsqlRefine.Rules.Tests.Style;

public sealed class NormalizeExecuteKeywordRuleTests
{
    private readonly NormalizeExecuteKeywordRule _rule = new();

    private static RuleContext CreateContext(string sql)
    {
        return RuleTestContext.CreateContext(sql);
    }

    [Fact]
    public void Analyze_ExecProcedure_ReturnsDiagnostic()
    {
        var sql = "EXEC sp_help;";
        var context = CreateContext(sql);

        var diagnostic = Assert.Single(_rule.Analyze(context));
        Assert.Equal("normalize-execute-keyword", diagnostic.Code);
        Assert.Equal("Use 'EXECUTE' instead of 'EXEC'.", diagnostic.Message);
    }

    [Fact]
    public void Analyze_ExecWithParentheses_ReturnsDiagnostic()
    {
        var sql = "EXEC(@sql);";
        var context = CreateContext(sql);

        var diagnostic = Assert.Single(_rule.Analyze(context));
        Assert.Equal("normalize-execute-keyword", diagnostic.Code);
    }

    [Fact]
    public void Analyze_ExecSpExecutesql_ReturnsDiagnostic()
    {
        var sql = "EXEC sp_executesql @sql;";
        var context = CreateContext(sql);

        var diagnostic = Assert.Single(_rule.Analyze(context));
        Assert.Equal("normalize-execute-keyword", diagnostic.Code);
    }

    [Fact]
    public void Analyze_MultipleExec_ReturnsMultipleDiagnostics()
    {
        var sql = "EXEC sp_help; EXEC sp_who;";
        var context = CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();
        Assert.Equal(2, diagnostics.Length);
    }

    [Fact]
    public void Analyze_ExecuteKeyword_NoDiagnostic()
    {
        var sql = "EXECUTE sp_help;";
        var context = CreateContext(sql);

        Assert.Empty(_rule.Analyze(context));
    }

    [Fact]
    public void Analyze_LowercaseExec_ReturnsDiagnostic()
    {
        var sql = "exec sp_help;";
        var context = CreateContext(sql);

        var diagnostic = Assert.Single(_rule.Analyze(context));
        Assert.Equal("normalize-execute-keyword", diagnostic.Code);
    }

    [Fact]
    public void GetFixes_Exec_ReturnsExecute()
    {
        var sql = "EXEC sp_help;";
        var context = CreateContext(sql);
        var diagnostic = _rule.Analyze(context).Single();

        var fix = Assert.Single(_rule.GetFixes(context, diagnostic));
        var edit = Assert.Single(fix.Edits);

        Assert.Equal("Use 'EXECUTE'", fix.Title);
        var updated = Apply(sql, edit);
        Assert.Equal("EXECUTE sp_help;", updated);
    }

    [Fact]
    public void GetFixes_ExecWithParentheses_ReturnsExecute()
    {
        var sql = "EXEC(@sql);";
        var context = CreateContext(sql);
        var diagnostic = _rule.Analyze(context).Single();

        var fix = Assert.Single(_rule.GetFixes(context, diagnostic));
        var edit = Assert.Single(fix.Edits);

        var updated = Apply(sql, edit);
        Assert.Equal("EXECUTE(@sql);", updated);
    }

    [Fact]
    public void Metadata_HasCorrectProperties()
    {
        Assert.Equal("normalize-execute-keyword", _rule.Metadata.RuleId);
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
