using System.IO;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules;

namespace TsqlRefine.Rules.Tests;

public sealed class CteNameConflictRuleTests
{
    [Theory]
    [InlineData("WITH cte AS (SELECT 1), cte AS (SELECT 2) SELECT * FROM cte")]  // duplicate CTE names
    [InlineData("WITH t AS (SELECT 1) SELECT * FROM Table1 t")]  // CTE name conflicts with table alias
    [InlineData("WITH CTE AS (SELECT 1), cte AS (SELECT 2) SELECT * FROM CTE")]  // case insensitive duplicate
    [InlineData("WITH a AS (SELECT 1), b AS (SELECT 2), a AS (SELECT 3) SELECT * FROM a")]  // third CTE duplicates first
    [InlineData("WITH myalias AS (SELECT 1) SELECT * FROM (SELECT * FROM t) myalias")]  // CTE conflicts with subquery alias
    public void Analyze_WhenCteNameConflict_ReturnsDiagnostic(string sql)
    {
        var rule = new CteNameConflictRule();
        var context = CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.NotEmpty(diagnostics);
        Assert.Contains(diagnostics, d => d.Data?.RuleId == "semantic/cte-name-conflict");
        Assert.All(diagnostics.Where(d => d.Data?.RuleId == "semantic/cte-name-conflict"), d =>
        {
            Assert.Equal("Correctness", d.Data?.Category);
            Assert.False(d.Data?.Fixable);
            Assert.Contains("CTE", d.Message, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Theory]
    [InlineData("WITH cte1 AS (SELECT 1), cte2 AS (SELECT 2) SELECT * FROM cte1 JOIN cte2")]  // valid CTEs
    [InlineData("WITH cte AS (SELECT 1) SELECT * FROM cte")]  // valid CTE reference
    [InlineData("WITH cte AS (SELECT 1) SELECT * FROM Table1 t")]  // CTE and different table alias
    [InlineData("SELECT * FROM Table1 t")]  // no CTE at all
    [InlineData("WITH cte AS (SELECT * FROM t) SELECT * FROM cte")]  // CTE references table
    [InlineData("WITH cte1 AS (SELECT 1), cte2 AS (SELECT * FROM cte1) SELECT * FROM cte2")]  // CTE references earlier CTE
    public void Analyze_WhenNoCteNameConflict_ReturnsEmpty(string sql)
    {
        var rule = new CteNameConflictRule();
        var context = CreateContext(sql);

        var diagnostics = rule.Analyze(context).Where(d => d.Data?.RuleId == "semantic/cte-name-conflict").ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_MultipleDuplicateCtesInDifferentQueries_ReportsEachSeparately()
    {
        var rule = new CteNameConflictRule();
        var sql = @"WITH a AS (SELECT 1), a AS (SELECT 2) SELECT * FROM a;
WITH b AS (SELECT 1), b AS (SELECT 2) SELECT * FROM b;";
        var context = CreateContext(sql);

        var diagnostics = rule.Analyze(context).Where(d => d.Data?.RuleId == "semantic/cte-name-conflict").ToArray();

        // Should report at least 2 conflicts (one for each query)
        Assert.True(diagnostics.Length >= 2, $"Expected at least 2 diagnostics, got {diagnostics.Length}");
    }

    [Fact]
    public void Analyze_CteInSubquery_DifferentScope_NoConflict()
    {
        var rule = new CteNameConflictRule();
        // CTE 'cte' in outer query and CTE 'cte' in subquery are different scopes
        var sql = "WITH cte AS (SELECT 1) SELECT (SELECT * FROM (WITH cte AS (SELECT 2) SELECT * FROM cte) x) FROM cte";
        var context = CreateContext(sql);

        var diagnostics = rule.Analyze(context).Where(d => d.Data?.RuleId == "semantic/cte-name-conflict").ToArray();

        // Different scopes, so no conflict
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_EmptyInput_ReturnsEmpty()
    {
        var rule = new CteNameConflictRule();
        var context = CreateContext("");

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void GetFixes_ReturnsEmpty()
    {
        var rule = new CteNameConflictRule();
        var context = CreateContext("WITH cte AS (SELECT 1), cte AS (SELECT 2) SELECT * FROM cte");
        var diagnostic = new Diagnostic(
            Range: new TsqlRefine.PluginSdk.Range(new Position(0, 0), new Position(0, 10)),
            Message: "test",
            Code: "semantic/cte-name-conflict"
        );

        var fixes = rule.GetFixes(context, diagnostic).ToArray();

        Assert.Empty(fixes);
    }

    [Fact]
    public void Metadata_HasCorrectValues()
    {
        var rule = new CteNameConflictRule();

        Assert.Equal("semantic/cte-name-conflict", rule.Metadata.RuleId);
        Assert.Equal("Correctness", rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Error, rule.Metadata.DefaultSeverity);
        Assert.False(rule.Metadata.Fixable);
        Assert.Contains("CTE", rule.Metadata.Description, StringComparison.OrdinalIgnoreCase);
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
