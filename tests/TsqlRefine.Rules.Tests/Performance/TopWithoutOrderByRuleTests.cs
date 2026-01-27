using System.IO;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules;
using TsqlRefine.Rules.Rules.Performance;

namespace TsqlRefine.Rules.Tests.Performance;

public sealed class TopWithoutOrderByRuleTests
{
    [Theory]
    [InlineData("SELECT TOP 10 * FROM users;")]
    [InlineData("SELECT TOP 5 id, name FROM orders;")]
    [InlineData("SELECT TOP (10) * FROM products;")]
    [InlineData("SELECT TOP 1 * FROM customers;")]
    [InlineData("select top 10 * from users;")]  // lowercase
    [InlineData("SELECT TOP 100 id FROM orders WHERE status = 'active';")]
    public void Analyze_WhenTopWithoutOrderBy_ReturnsDiagnostic(string sql)
    {
        var rule = new TopWithoutOrderByRule();
        var context = CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("top-without-order-by", diagnostics[0].Data?.RuleId);
        Assert.Equal("Performance", diagnostics[0].Data?.Category);
        Assert.False(diagnostics[0].Data?.Fixable);
        Assert.Contains("ORDER BY", diagnostics[0].Message);
    }

    [Theory]
    [InlineData("SELECT TOP 10 * FROM users ORDER BY id;")]
    [InlineData("SELECT TOP 5 name FROM orders ORDER BY created_at DESC;")]
    [InlineData("SELECT TOP (10) * FROM products ORDER BY price, name;")]
    [InlineData("SELECT TOP 1 * FROM customers ORDER BY last_login DESC;")]
    [InlineData("SELECT * FROM users;")]  // no TOP
    [InlineData("SELECT * FROM users ORDER BY id;")]  // no TOP
    public void Analyze_WhenNotViolating_ReturnsEmpty(string sql)
    {
        var rule = new TopWithoutOrderByRule();
        var context = CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_TopWithoutOrderBy_ReportsAtTopClause()
    {
        var rule = new TopWithoutOrderByRule();
        var sql = "SELECT TOP 10 * FROM users;";
        var context = CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        var diagnostic = diagnostics[0];

        // TOP keyword should be reported
        Assert.Equal(0, diagnostic.Range.Start.Line);
        // TOP starts after "SELECT " (7 characters)
        Assert.Equal(7, diagnostic.Range.Start.Character);
    }

    [Fact]
    public void Analyze_MultipleViolations_ReturnsMultipleDiagnostics()
    {
        var rule = new TopWithoutOrderByRule();
        var sql = @"SELECT TOP 10 * FROM users;
SELECT TOP 5 * FROM orders;";
        var context = CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Equal(2, diagnostics.Length);
        Assert.All(diagnostics, d => Assert.Equal("top-without-order-by", d.Data?.RuleId));
    }

    [Fact]
    public void Analyze_TopWithPercent_WithoutOrderBy_ReturnsDiagnostic()
    {
        var rule = new TopWithoutOrderByRule();
        var sql = "SELECT TOP 10 PERCENT * FROM users;";
        var context = CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("top-without-order-by", diagnostics[0].Data?.RuleId);
    }

    [Fact]
    public void Analyze_TopWithPercent_WithOrderBy_ReturnsEmpty()
    {
        var rule = new TopWithoutOrderByRule();
        var sql = "SELECT TOP 10 PERCENT * FROM users ORDER BY id;";
        var context = CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_NestedQueries_ValidatesEachIndependently()
    {
        var rule = new TopWithoutOrderByRule();
        var sql = @"SELECT TOP 10 * FROM (
    SELECT TOP 5 * FROM users ORDER BY id
) AS subquery;";
        var context = CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        // Only the outer query should be flagged (inner has ORDER BY)
        Assert.Single(diagnostics);
        Assert.Equal("top-without-order-by", diagnostics[0].Data?.RuleId);
    }

    [Fact]
    public void Analyze_EmptyInput_ReturnsEmpty()
    {
        var rule = new TopWithoutOrderByRule();
        var context = CreateContext("");

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void GetFixes_ReturnsEmpty()
    {
        var rule = new TopWithoutOrderByRule();
        var context = CreateContext("SELECT TOP 10 * FROM users;");
        var diagnostic = new Diagnostic(
            Range: new TsqlRefine.PluginSdk.Range(new Position(0, 7), new Position(0, 13)),
            Message: "test",
            Code: "top-without-order-by"
        );

        var fixes = rule.GetFixes(context, diagnostic).ToArray();

        Assert.Empty(fixes);
    }

    [Fact]
    public void Metadata_HasCorrectValues()
    {
        var rule = new TopWithoutOrderByRule();

        Assert.Equal("top-without-order-by", rule.Metadata.RuleId);
        Assert.Equal("Performance", rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Error, rule.Metadata.DefaultSeverity);
        Assert.False(rule.Metadata.Fixable);
        Assert.Contains("TOP", rule.Metadata.Description);
        Assert.Contains("ORDER BY", rule.Metadata.Description);
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
