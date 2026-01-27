using System.IO;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules;
using TsqlRefine.Rules.Rules.Correctness;

namespace TsqlRefine.Rules.Tests.Correctness;

public sealed class AliasScopeViolationRuleTests
{
    [Theory]
    [InlineData("SELECT * FROM (SELECT * FROM t1 WHERE t2.id = 1) x JOIN t2 ON 1=1")]  // Inner references t2 before defined
    [InlineData("SELECT * FROM (SELECT t3.col FROM t1) x, t2, t3")]  // Subquery references t3 from outer scope (order issue)
    public void Analyze_WhenAliasScopeViolation_ReturnsDiagnostic(string sql)
    {
        var rule = new AliasScopeViolationRule();
        var context = CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.NotEmpty(diagnostics);
        Assert.Contains(diagnostics, d => d.Data?.RuleId == "semantic/alias-scope-violation");
        Assert.All(diagnostics.Where(d => d.Data?.RuleId == "semantic/alias-scope-violation"), d =>
        {
            Assert.Equal("Correctness", d.Data?.Category);
            Assert.False(d.Data?.Fixable);
        });
    }

    [Theory]
    [InlineData("SELECT * FROM t1 WHERE EXISTS (SELECT 1 FROM t2 WHERE t2.id = t1.id)")]  // Valid correlation
    [InlineData("SELECT * FROM (SELECT * FROM t1) x JOIN t2 ON x.id = t2.id")]  // Valid reference
    [InlineData("SELECT * FROM t1, (SELECT * FROM t2) x WHERE t1.id = x.id")]  // Valid reference
    [InlineData("SELECT * FROM t1 WHERE t1.id IN (SELECT t2.id FROM t2)")]  // Valid subquery
    [InlineData("SELECT * FROM (SELECT * FROM t1 WHERE t1.id = 1) x")]  // Self-reference in subquery
    public void Analyze_WhenNoAliasScopeViolation_ReturnsEmpty(string sql)
    {
        var rule = new AliasScopeViolationRule();
        var context = CreateContext(sql);

        var diagnostics = rule.Analyze(context).Where(d => d.Data?.RuleId == "semantic/alias-scope-violation").ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_CorrelatedSubquery_NoViolation()
    {
        var rule = new AliasScopeViolationRule();
        // This is a valid correlated subquery - outer table referenced in inner WHERE
        var sql = "SELECT * FROM orders o WHERE EXISTS (SELECT 1 FROM order_items oi WHERE oi.order_id = o.id)";
        var context = CreateContext(sql);

        var diagnostics = rule.Analyze(context).Where(d => d.Data?.RuleId == "semantic/alias-scope-violation").ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_EmptyInput_ReturnsEmpty()
    {
        var rule = new AliasScopeViolationRule();
        var context = CreateContext("");

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void GetFixes_ReturnsEmpty()
    {
        var rule = new AliasScopeViolationRule();
        var context = CreateContext("SELECT * FROM (SELECT * FROM t1 WHERE t2.id = 1) x JOIN t2 ON 1=1");
        var diagnostic = new Diagnostic(
            Range: new TsqlRefine.PluginSdk.Range(new Position(0, 0), new Position(0, 10)),
            Message: "test",
            Code: "semantic/alias-scope-violation"
        );

        var fixes = rule.GetFixes(context, diagnostic).ToArray();

        Assert.Empty(fixes);
    }

    [Fact]
    public void Metadata_HasCorrectValues()
    {
        var rule = new AliasScopeViolationRule();

        Assert.Equal("semantic/alias-scope-violation", rule.Metadata.RuleId);
        Assert.Equal("Correctness", rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Warning, rule.Metadata.DefaultSeverity);
        Assert.False(rule.Metadata.Fixable);
        Assert.Contains("scope", rule.Metadata.Description, StringComparison.OrdinalIgnoreCase);
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
