using System.IO;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules;
using TsqlRefine.Rules.Rules.Performance;

namespace TsqlRefine.Rules.Tests.Performance;

public sealed class FullTextRuleTests
{
    [Fact]
    public void Metadata_ShouldHaveCorrectProperties()
    {
        var rule = new FullTextRule();

        Assert.Equal("full-text", rule.Metadata.RuleId);
        Assert.Equal("Performance", rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Information, rule.Metadata.DefaultSeverity);
        Assert.False(rule.Metadata.Fixable);
    }

    [Theory]
    [InlineData("SELECT * FROM documents WHERE CONTAINS(content, 'search term')")]
    [InlineData("SELECT * FROM articles WHERE FREETEXT(body, 'search phrase')")]
    public void Analyze_WhenFullTextPredicateUsed_ReturnsDiagnostic(string sql)
    {
        var rule = new FullTextRule();
        var context = CreateContext(sql);
        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("full-text", diagnostics[0].Code);
        Assert.Contains("full-text", diagnostics[0].Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("SELECT * FROM documents WHERE title LIKE '%search%'")]
    [InlineData("SELECT * FROM articles WHERE body = 'exact match'")]
    [InlineData("SELECT * FROM products WHERE name LIKE 'test%'")]
    public void Analyze_WhenNoFullTextPredicate_ReturnsNoDiagnostic(string sql)
    {
        var rule = new FullTextRule();
        var context = CreateContext(sql);
        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_WhenMultipleFullTextPredicates_ReturnsMultipleDiagnostics()
    {
        var sql = @"
            SELECT * FROM documents
            WHERE CONTAINS(content, 'term1')
               OR FREETEXT(title, 'term2')
        ";

        var rule = new FullTextRule();
        var context = CreateContext(sql);
        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Equal(2, diagnostics.Length);
        Assert.All(diagnostics, d => Assert.Equal("full-text", d.Code));
    }

    [Theory]
    [InlineData("SELECT * FROM CONTAINSTABLE(documents, content, 'search')")]
    [InlineData("SELECT * FROM FREETEXTTABLE(articles, *, 'search')")]
    public void Analyze_WhenFullTextTableFunction_ReturnsDiagnostic(string sql)
    {
        var rule = new FullTextRule();
        var context = CreateContext(sql);
        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("full-text", diagnostics[0].Code);
    }

    [Fact]
    public void GetFixes_ReturnsNoFixes()
    {
        var rule = new FullTextRule();
        var context = CreateContext("SELECT * FROM documents WHERE CONTAINS(content, 'search')");
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
                    text.Length,
                    token.TokenType.ToString());
            })
            .ToArray();
    }
}
