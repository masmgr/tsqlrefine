using System.IO;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules;

namespace TsqlRefine.Rules.Tests;

public sealed class ConditionalBeginEndRuleTests
{
    [Fact]
    public void Metadata_ShouldHaveCorrectProperties()
    {
        var rule = new ConditionalBeginEndRule();

        Assert.Equal("conditional-begin-end", rule.Metadata.RuleId);
        Assert.Equal("Style", rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Information, rule.Metadata.DefaultSeverity);
        Assert.False(rule.Metadata.Fixable);
    }

    [Theory]
    [InlineData("IF @x = 1 SELECT 1")]
    [InlineData("IF @x > 0 UPDATE users SET active = 1")]
    [InlineData(@"
        IF @x = 1
            SELECT 1
        ELSE
            SELECT 2")]
    [InlineData(@"
        IF @x > 0
            PRINT 'positive'")]
    public void Analyze_WhenIfWithoutBeginEnd_ReturnsDiagnostic(string sql)
    {
        var rule = new ConditionalBeginEndRule();
        var context = CreateContext(sql);
        var diagnostics = rule.Analyze(context).ToArray();

        Assert.NotEmpty(diagnostics);
        Assert.All(diagnostics, d => Assert.Equal("conditional-begin-end", d.Code));
        Assert.All(diagnostics, d => Assert.Contains("BEGIN", d.Message));
    }

    [Theory]
    [InlineData(@"
        IF @x = 1
        BEGIN
            SELECT 1
        END")]
    [InlineData(@"
        IF @x = 1
        BEGIN
            SELECT 1
        END
        ELSE
        BEGIN
            SELECT 2
        END")]
    [InlineData(@"
        IF @x > 0
        BEGIN
            UPDATE users SET active = 1
        END")]
    public void Analyze_WhenIfWithBeginEnd_ReturnsNoDiagnostic(string sql)
    {
        var rule = new ConditionalBeginEndRule();
        var context = CreateContext(sql);
        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_WhenElseWithoutBeginEnd_ReturnsDiagnostic()
    {
        var sql = @"
            IF @x = 1
            BEGIN
                SELECT 1
            END
            ELSE
                SELECT 2";

        var rule = new ConditionalBeginEndRule();
        var context = CreateContext(sql);
        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("conditional-begin-end", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_WhenMultipleIfWithoutBeginEnd_ReturnsMultipleDiagnostics()
    {
        var sql = @"
            IF @x = 1 SELECT 1
            IF @y = 2 SELECT 2
        ";

        var rule = new ConditionalBeginEndRule();
        var context = CreateContext(sql);
        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Equal(2, diagnostics.Length);
        Assert.All(diagnostics, d => Assert.Equal("conditional-begin-end", d.Code));
    }

    [Fact]
    public void GetFixes_ReturnsNoFixes()
    {
        var rule = new ConditionalBeginEndRule();
        var context = CreateContext("IF @x = 1 SELECT 1");
        var diagnostic = rule.Analyze(context).First();
        var fixes = rule.GetFixes(context, diagnostic).ToArray();

        Assert.Empty(fixes);
    }

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
                    text.Length);
            })
            .ToArray();
    }
}
