using System.IO;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules;
using TsqlRefine.Rules.Rules.Correctness;
using Xunit;

namespace TsqlRefine.Rules.Tests.Correctness;

public sealed class EscapeKeywordIdentifierRuleTests
{
    private readonly EscapeKeywordIdentifierRule _rule = new();

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
    public void Analyze_TableNameKeywordAfterFrom_ReturnsDiagnostic()
    {
        var sql = "SELECT * FROM order;";
        var context = CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToList();

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("escape-keyword-identifier", diagnostic.Code);
        Assert.Contains("[order]", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Analyze_QualifiedColumnKeywordAfterDot_ReturnsDiagnostic()
    {
        var sql = "SELECT t.order FROM t;";
        var context = CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToList();

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("escape-keyword-identifier", diagnostic.Code);
        Assert.Contains("[order]", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Analyze_CreateTableColumnNameKeyword_ReturnsDiagnostic()
    {
        var sql = "CREATE TABLE t (order int);";
        var context = CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToList();

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("escape-keyword-identifier", diagnostic.Code);
        Assert.Contains("[order]", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Analyze_AlreadyEscapedIdentifier_NoDiagnostic()
    {
        var sql = "SELECT * FROM [order];";
        var context = CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToList();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_TableValuedFunction_NoDiagnostic()
    {
        var sql = "SELECT * FROM OPENJSON(@j);";
        var context = CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToList();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_CreateTablePrimaryKeyConstraint_NoDiagnostic()
    {
        var sql = "CREATE TABLE t (id int, PRIMARY KEY (id));";
        var context = CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToList();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void GetFixes_ReturnsBracketEscapeEdit()
    {
        var sql = "SELECT * FROM order;";
        var context = CreateContext(sql);
        var diagnostic = _rule.Analyze(context).Single();

        var fixes = _rule.GetFixes(context, diagnostic).ToList();

        var fix = Assert.Single(fixes);
        Assert.Equal("Escape keyword identifier", fix.Title);
        var edit = Assert.Single(fix.Edits);
        Assert.Equal(diagnostic.Range, edit.Range);
        Assert.Equal("[order]", edit.NewText);
    }

    [Fact]
    public void Metadata_HasCorrectProperties()
    {
        Assert.Equal("escape-keyword-identifier", _rule.Metadata.RuleId);
        Assert.Equal("Correctness", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Warning, _rule.Metadata.DefaultSeverity);
        Assert.True(_rule.Metadata.Fixable);
    }
}
