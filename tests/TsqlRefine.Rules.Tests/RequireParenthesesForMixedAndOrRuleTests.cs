using System.IO;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules;

namespace TsqlRefine.Rules.Tests;

public sealed class RequireParenthesesForMixedAndOrRuleTests
{
    [Theory]
    [InlineData("SELECT * FROM users WHERE active = 1 AND status = 'ok' OR role = 'admin';")]
    [InlineData("SELECT * FROM users WHERE a = 1 OR b = 2 AND c = 3;")]
    [InlineData("DELETE FROM logs WHERE level = 'debug' AND age > 30 OR size > 1000;")]
    [InlineData("UPDATE users SET active = 1 WHERE status = 'new' AND created_at < '2020-01-01' OR role = 'guest';")]
    public void Analyze_WhenMixedAndOrWithoutParentheses_ReturnsDiagnostic(string sql)
    {
        var rule = new RequireParenthesesForMixedAndOrRule();
        var context = CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.NotEmpty(diagnostics);
        Assert.All(diagnostics, d =>
        {
            Assert.Equal("require-parentheses-for-mixed-and-or", d.Data?.RuleId);
            Assert.Equal("Correctness", d.Data?.Category);
            Assert.False(d.Data?.Fixable);
            Assert.Contains("parentheses", d.Message, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Theory]
    [InlineData("SELECT * FROM users WHERE (active = 1 AND status = 'ok') OR role = 'admin';")]
    [InlineData("SELECT * FROM users WHERE active = 1 AND status = 'ok' AND role = 'admin';")]
    [InlineData("SELECT * FROM users WHERE active = 1 OR status = 'ok' OR role = 'admin';")]
    [InlineData("SELECT * FROM users WHERE (a = 1 OR b = 2) AND c = 3;")]
    [InlineData("SELECT * FROM users WHERE a = 1 OR (b = 2 AND c = 3);")]
    [InlineData("SELECT * FROM users WHERE ((a AND b) OR (c AND d));")]
    [InlineData("SELECT * FROM users WHERE active = 1;")]  // Single condition
    [InlineData("SELECT * FROM users WHERE a AND b;")]  // Only AND
    [InlineData("SELECT * FROM users WHERE a OR b;")]  // Only OR
    [InlineData("SELECT * FROM users WHERE (a OR b OR c) AND (d OR e);")]  // Proper grouping
    public void Analyze_WhenNotViolating_ReturnsEmpty(string sql)
    {
        var rule = new RequireParenthesesForMixedAndOrRule();
        var context = CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_SimpleMixedAndOr_ReportsAtOperator()
    {
        var rule = new RequireParenthesesForMixedAndOrRule();
        var sql = "SELECT * FROM users WHERE active = 1 AND status = 'ok' OR role = 'admin';";
        var context = CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        var diagnostic = diagnostics[0];

        // The diagnostic should point to the OR operator that mixes with AND
        Assert.Equal(0, diagnostic.Range.Start.Line);
        Assert.True(diagnostic.Range.Start.Character > 0);
    }

    [Fact]
    public void Analyze_MultipleViolations_ReturnsMultipleDiagnostics()
    {
        var rule = new RequireParenthesesForMixedAndOrRule();
        var sql = @"SELECT * FROM t WHERE a = 1 AND b = 2 OR c = 3;
SELECT * FROM t WHERE x = 1 OR y = 2 AND z = 3;";
        var context = CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Equal(2, diagnostics.Length);
        Assert.All(diagnostics, d => Assert.Equal("require-parentheses-for-mixed-and-or", d.Data?.RuleId));
    }

    [Fact]
    public void Analyze_NestedParentheses_RespectsParenthesisBoundaries()
    {
        var rule = new RequireParenthesesForMixedAndOrRule();
        var sql = "SELECT * FROM users WHERE (a AND b) OR (c AND d);";
        var context = CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        // Should not report because parentheses properly separate the operators
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_ComplexNesting_HandlesCorrectly()
    {
        var rule = new RequireParenthesesForMixedAndOrRule();
        var sql = "SELECT * FROM users WHERE ((a AND b) OR c) AND d;";
        var context = CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        // Should not report because parentheses properly separate the operators
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_EmptyInput_ReturnsEmpty()
    {
        var rule = new RequireParenthesesForMixedAndOrRule();
        var context = CreateContext("");

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void GetFixes_ReturnsEmpty()
    {
        var rule = new RequireParenthesesForMixedAndOrRule();
        var context = CreateContext("SELECT * FROM users WHERE a AND b OR c;");
        var diagnostic = new Diagnostic(
            Range: new TsqlRefine.PluginSdk.Range(new Position(0, 0), new Position(0, 10)),
            Message: "test",
            Code: "require-parentheses-for-mixed-and-or"
        );

        var fixes = rule.GetFixes(context, diagnostic).ToArray();

        Assert.Empty(fixes);
    }

    [Fact]
    public void Metadata_HasCorrectValues()
    {
        var rule = new RequireParenthesesForMixedAndOrRule();

        Assert.Equal("require-parentheses-for-mixed-and-or", rule.Metadata.RuleId);
        Assert.Equal("Correctness", rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Warning, rule.Metadata.DefaultSeverity);
        Assert.False(rule.Metadata.Fixable);
        Assert.Contains("AND", rule.Metadata.Description);
        Assert.Contains("OR", rule.Metadata.Description);
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
