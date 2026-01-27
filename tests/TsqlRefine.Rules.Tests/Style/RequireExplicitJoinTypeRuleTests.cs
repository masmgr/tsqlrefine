using System.IO;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules;
using TsqlRefine.Rules.Rules.Style;
using Xunit;

namespace TsqlRefine.Rules.Tests.Style;

public sealed class RequireExplicitJoinTypeRuleTests
{
    private readonly RequireExplicitJoinTypeRule _rule = new();

    private static RuleContext CreateContext(string sql)
    {
        var parser = new TSql150Parser(initialQuotedIdentifiers: true);
        using var reader = new StringReader(sql);
        var fragment = parser.Parse(reader, out var parseErrors);

        var ast = new ScriptDomAst(sql, fragment, parseErrors as IReadOnlyList<ParseError>, Array.Empty<ParseError>());
        var tokens = Tokenize(sql);

        return new RuleContext(
            FilePath: "<test>",
            CompatLevel: 150,
            Ast: ast,
            Tokens: tokens,
            Settings: new RuleSettings()
        );
    }

    private static IReadOnlyList<Token> Tokenize(string sql)
    {
        var parser = new TSql150Parser(initialQuotedIdentifiers: true);
        using var reader = new StringReader(sql);
        var tokenStream = parser.GetTokenStream(reader, out _);
        return tokenStream
            .Where(token => token.TokenType != TSqlTokenType.EndOfFile)
            .Select(token =>
            {
                var text = token.Text ?? string.Empty;
                return new Token(
                    text,
                    new Position(Math.Max(0, token.Line - 1), Math.Max(0, token.Column - 1)),
                    text.Length,
                    token.TokenType.ToString());
            })
            .ToArray();
    }

    [Fact]
    public void Analyze_ImplicitInnerJoin_ReturnsDiagnostic()
    {
        var sql = "SELECT * FROM dbo.TableA JOIN dbo.TableB ON TableA.Id = TableB.Id;";
        var context = CreateContext(sql);

        var diagnostic = Assert.Single(_rule.Analyze(context));
        Assert.Equal("require-explicit-join-type", diagnostic.Code);
        Assert.Equal(
            "JOIN must be explicit: use INNER JOIN, LEFT OUTER JOIN, RIGHT OUTER JOIN, or FULL OUTER JOIN.",
            diagnostic.Message);
    }

    [Fact]
    public void Analyze_ImplicitOuterJoin_ReturnsDiagnostic()
    {
        var sql = "SELECT * FROM dbo.TableA LEFT JOIN dbo.TableB ON TableA.Id = TableB.Id;";
        var context = CreateContext(sql);

        var diagnostic = Assert.Single(_rule.Analyze(context));
        Assert.Equal("require-explicit-join-type", diagnostic.Code);
    }

    [Fact]
    public void Analyze_ExplicitJoinTypes_NoDiagnostics()
    {
        var sql = """
                  SELECT *
                  FROM dbo.TableA
                  INNER JOIN dbo.TableB ON TableA.Id = TableB.Id
                  LEFT OUTER JOIN dbo.TableC ON TableA.Id = TableC.Id
                  RIGHT OUTER JOIN dbo.TableD ON TableA.Id = TableD.Id
                  FULL OUTER JOIN dbo.TableE ON TableA.Id = TableE.Id;
                  """;
        var context = CreateContext(sql);

        Assert.Empty(_rule.Analyze(context));
    }

    [Fact]
    public void Analyze_CrossJoin_NoDiagnostics()
    {
        var sql = "SELECT * FROM dbo.TableA CROSS JOIN dbo.TableB;";
        var context = CreateContext(sql);

        Assert.Empty(_rule.Analyze(context));
    }

    [Fact]
    public void Analyze_CommaSeparatedTables_NoDiagnostics()
    {
        var sql = "SELECT * FROM dbo.TableA, dbo.TableB;";
        var context = CreateContext(sql);

        Assert.Empty(_rule.Analyze(context));
    }

    [Fact]
    public void GetFixes_ImplicitInnerJoin_InsertsInnerKeyword()
    {
        var sql = "SELECT * FROM dbo.TableA JOIN dbo.TableB ON TableA.Id = TableB.Id;";
        var context = CreateContext(sql);
        var diagnostic = _rule.Analyze(context).Single();

        var fix = Assert.Single(_rule.GetFixes(context, diagnostic));
        var edit = Assert.Single(fix.Edits);

        var updated = Apply(sql, edit);
        Assert.Equal("SELECT * FROM dbo.TableA INNER JOIN dbo.TableB ON TableA.Id = TableB.Id;", updated);
    }

    [Fact]
    public void GetFixes_LeftJoinWithoutOuter_InsertsOuterKeyword()
    {
        var sql = "SELECT * FROM dbo.TableA LEFT JOIN dbo.TableB ON TableA.Id = TableB.Id;";
        var context = CreateContext(sql);
        var diagnostic = _rule.Analyze(context).Single();

        var fix = Assert.Single(_rule.GetFixes(context, diagnostic));
        var edit = Assert.Single(fix.Edits);

        var updated = Apply(sql, edit);
        Assert.Equal("SELECT * FROM dbo.TableA LEFT OUTER JOIN dbo.TableB ON TableA.Id = TableB.Id;", updated);
    }

    [Fact]
    public void GetFixes_LeftJoinHintWithoutOuter_InsertsOuterBeforeHint()
    {
        var sql = "SELECT * FROM dbo.TableA LEFT HASH JOIN dbo.TableB ON TableA.Id = TableB.Id;";
        var context = CreateContext(sql);
        var diagnostic = _rule.Analyze(context).Single();

        var fix = Assert.Single(_rule.GetFixes(context, diagnostic));
        var edit = Assert.Single(fix.Edits);

        var updated = Apply(sql, edit);
        Assert.Equal("SELECT * FROM dbo.TableA LEFT OUTER HASH JOIN dbo.TableB ON TableA.Id = TableB.Id;", updated);
    }

    [Fact]
    public void Metadata_HasCorrectProperties()
    {
        Assert.Equal("require-explicit-join-type", _rule.Metadata.RuleId);
        Assert.Equal("Style", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Warning, _rule.Metadata.DefaultSeverity);
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
