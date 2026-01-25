using System.IO;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules;

namespace TsqlRefine.Rules.Tests;

public sealed class DuplicateGoRuleTests
{
    [Fact]
    public void Analyze_WhenConsecutiveGo_ReturnsDiagnostic()
    {
        var rule = new DuplicateGoRule();
        var sql = "SELECT 1;\nGO\nGO\nSELECT 2;";
        var context = new RuleContext(
            FilePath: "<test>",
            CompatLevel: 150,
            Ast: new ScriptDomAst(sql),
            Tokens: Tokenize(sql),
            Settings: new RuleSettings()
        );

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.NotEmpty(diagnostics);
        Assert.All(diagnostics, d => Assert.Equal("duplicate-go", d.Data?.RuleId));
    }

    [Fact]
    public void Analyze_WhenSingleGo_ReturnsEmpty()
    {
        var rule = new DuplicateGoRule();
        var sql = "SELECT 1;\nGO\nSELECT 2;";
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
    public void Analyze_WhenGoWithEmptyLinesBetween_ReturnsDiagnostic()
    {
        var rule = new DuplicateGoRule();
        var sql = "SELECT 1;\nGO\n\nGO\nSELECT 2;";
        var context = new RuleContext(
            FilePath: "<test>",
            CompatLevel: 150,
            Ast: new ScriptDomAst(sql),
            Tokens: Tokenize(sql),
            Settings: new RuleSettings()
        );

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.NotEmpty(diagnostics);
    }

    [Fact]
    public void Analyze_WhenNoGo_ReturnsEmpty()
    {
        var rule = new DuplicateGoRule();
        var sql = "SELECT 1;\nSELECT 2;";
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
    public void Analyze_WhenGoInComment_ReturnsEmpty()
    {
        var rule = new DuplicateGoRule();
        var sql = "-- GO\nSELECT 1;\nGO";
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
