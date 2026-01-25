using System.IO;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules;

namespace TsqlRefine.Rules.Tests;

public sealed class NestedBlockCommentsRuleTests
{
    [Fact]
    public void Analyze_WhenNestedBlockComments_ReturnsDiagnostic()
    {
        var rule = new NestedBlockCommentsRule();
        var sql = "/* outer /* inner */ outer */\nSELECT 1;";
        var context = new RuleContext(
            FilePath: "<test>",
            CompatLevel: 150,
            Ast: new ScriptDomAst(sql),
            Tokens: Tokenize(sql),
            Settings: new RuleSettings()
        );

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.NotEmpty(diagnostics);
        Assert.All(diagnostics, d => Assert.Equal("nested-block-comments", d.Data?.RuleId));
    }

    [Fact]
    public void Analyze_WhenSimpleBlockComment_ReturnsEmpty()
    {
        var rule = new NestedBlockCommentsRule();
        var sql = "/* simple comment */\nSELECT 1;";
        var context = new RuleContext(
            FilePath: "<test>",
            CompatLevel: 150,
            Ast: new ScriptDomAst(sql),
            Tokens: Tokenize(sql),
            Settings: new RuleSettings()
        );

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_WhenNoComments_ReturnsEmpty()
    {
        var rule = new NestedBlockCommentsRule();
        var sql = "SELECT 1;";
        var context = new RuleContext(
            FilePath: "<test>",
            CompatLevel: 150,
            Ast: new ScriptDomAst(sql),
            Tokens: Tokenize(sql),
            Settings: new RuleSettings()
        );

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_WhenLineComment_ReturnsEmpty()
    {
        var rule = new NestedBlockCommentsRule();
        var sql = "-- line comment\nSELECT 1;";
        var context = new RuleContext(
            FilePath: "<test>",
            CompatLevel: 150,
            Ast: new ScriptDomAst(sql),
            Tokens: Tokenize(sql),
            Settings: new RuleSettings()
        );

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_WhenMultipleBlockComments_ReturnsEmpty()
    {
        var rule = new NestedBlockCommentsRule();
        var sql = "/* comment 1 */ SELECT 1; /* comment 2 */";
        var context = new RuleContext(
            FilePath: "<test>",
            CompatLevel: 150,
            Ast: new ScriptDomAst(sql),
            Tokens: Tokenize(sql),
            Settings: new RuleSettings()
        );

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
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
}
