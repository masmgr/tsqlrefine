using System.IO;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules;

namespace TsqlRefine.Rules.Tests;

public sealed class AvoidSelectStarRuleTests
{
    [Fact]
    public void Analyze_WhenSelectStar_ReturnsDiagnostic()
    {
        var rule = new AvoidSelectStarRule();
        var sql = "select * from t;";
        var context = new RuleContext(
            FilePath: "<test>",
            CompatLevel: 150,
            Ast: new ScriptDomAst(sql),
            Tokens: Tokenize(sql),
            Settings: new RuleSettings()
        );

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("avoid-select-star", diagnostics[0].Data?.RuleId);
        Assert.Equal(0, diagnostics[0].Range.Start.Line);
        Assert.Equal(7, diagnostics[0].Range.Start.Character);
        Assert.Equal(0, diagnostics[0].Range.End.Line);
        Assert.Equal(8, diagnostics[0].Range.End.Character);
    }

    [Fact]
    public void Analyze_WhenNoSelectStar_ReturnsEmpty()
    {
        var rule = new AvoidSelectStarRule();
        var sql = "select id from t;";
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
