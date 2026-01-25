using System.IO;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules;

namespace TsqlRefine.Rules.Tests;

public sealed class KeywordCapitalizationRuleTests
{
    [Fact]
    public void Analyze_WhenKeywordLowercase_ReturnsDiagnostic()
    {
        var rule = new KeywordCapitalizationRule();
        var sql = "select id from users;";
        var context = new RuleContext(
            FilePath: "<test>",
            CompatLevel: 150,
            Ast: new ScriptDomAst(sql),
            Tokens: Tokenize(sql),
            Settings: new RuleSettings()
        );

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.NotEmpty(diagnostics);
        Assert.Contains(diagnostics, d => d.Message.Contains("SELECT"));
        Assert.Contains(diagnostics, d => d.Message.Contains("FROM"));
        Assert.All(diagnostics, d => Assert.Equal("keyword-capitalization", d.Data?.RuleId));
    }

    [Fact]
    public void Analyze_WhenKeywordUppercase_ReturnsEmpty()
    {
        var rule = new KeywordCapitalizationRule();
        var sql = "SELECT id FROM users;";
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
    public void Analyze_WhenKeywordMixedCase_ReturnsDiagnostic()
    {
        var rule = new KeywordCapitalizationRule();
        var sql = "SeLeCt id FrOm users;";
        var context = new RuleContext(
            FilePath: "<test>",
            CompatLevel: 150,
            Ast: new ScriptDomAst(sql),
            Tokens: Tokenize(sql),
            Settings: new RuleSettings()
        );

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.NotEmpty(diagnostics);
        Assert.Contains(diagnostics, d => d.Message.Contains("SeLeCt"));
        Assert.Contains(diagnostics, d => d.Message.Contains("FrOm"));
    }

    [Fact]
    public void Analyze_WhenNoKeywords_ReturnsEmpty()
    {
        var rule = new KeywordCapitalizationRule();
        var sql = "";
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
