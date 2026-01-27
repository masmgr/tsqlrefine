using System.IO;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules;
using TsqlRefine.Rules.Rules.Transactions;

namespace TsqlRefine.Rules.Tests.Transactions;

public sealed class SetAnsiRuleTests
{
    [Fact]
    public void Analyze_WhenSetAnsiNullsOnPresent_ReturnsEmpty()
    {
        var rule = new SetAnsiRule();
        var sql = "SET ANSI_NULLS ON;\nGO\nSELECT 1;";
        var context = new RuleContext(
            FilePath: "<test>",
            CompatLevel: 150,
            Ast: ParseSql(sql),
            Tokens: Tokenize(sql),
            Settings: new RuleSettings()
        );

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_WhenSetAnsiNullsMissing_ReturnsDiagnostic()
    {
        var rule = new SetAnsiRule();
        var sql = "CREATE PROCEDURE dbo.Test AS BEGIN SELECT 1; END;";
        var context = new RuleContext(
            FilePath: "<test>",
            CompatLevel: 150,
            Ast: ParseSql(sql),
            Tokens: Tokenize(sql),
            Settings: new RuleSettings()
        );

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.NotEmpty(diagnostics);
        Assert.All(diagnostics, d => Assert.Equal("set-ansi", d.Data?.RuleId));
    }

    [Fact]
    public void Analyze_WhenSetAnsiNullsOff_ReturnsDiagnostic()
    {
        var rule = new SetAnsiRule();
        var sql = "SET ANSI_NULLS OFF;\nGO\nCREATE PROCEDURE dbo.Test AS BEGIN SELECT 1; END;";
        var context = new RuleContext(
            FilePath: "<test>",
            CompatLevel: 150,
            Ast: ParseSql(sql),
            Tokens: Tokenize(sql),
            Settings: new RuleSettings()
        );

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.NotEmpty(diagnostics);
    }

    [Fact]
    public void Analyze_WhenSetAnsiNullsTooLate_ReturnsDiagnostic()
    {
        var rule = new SetAnsiRule();
        var sql = string.Join("\n",
            "SELECT 1;",
            "SELECT 2;",
            "SELECT 3;",
            "SELECT 4;",
            "SELECT 5;",
            "SELECT 6;",
            "SELECT 7;",
            "SELECT 8;",
            "SELECT 9;",
            "SELECT 10;",
            "SELECT 11;",
            "SET ANSI_NULLS ON;"
        );
        var context = new RuleContext(
            FilePath: "<test>",
            CompatLevel: 150,
            Ast: ParseSql(sql),
            Tokens: Tokenize(sql),
            Settings: new RuleSettings()
        );

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.NotEmpty(diagnostics);
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

    private static ScriptDomAst ParseSql(string sql)
    {
        var parser = new TSql150Parser(initialQuotedIdentifiers: true);
        using var reader = new StringReader(sql);
        var fragment = parser.Parse(reader, out IList<ParseError> errors);
        return new ScriptDomAst(sql, fragment, errors.ToArray(), Array.Empty<ParseError>());
    }
}
