using System.IO;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules;

namespace TsqlRefine.Rules.Tests;

public sealed class JoinConditionAlwaysTrueRuleTests
{
    [Theory]
    [InlineData("SELECT * FROM t1 JOIN t2 ON 1=1")]  // always true: 1=1
    [InlineData("SELECT * FROM t1 JOIN t2 ON 0=0")]  // always true: 0=0
    [InlineData("SELECT * FROM t1 JOIN t2 ON 'a'='a'")]  // always true: string literals
    [InlineData("SELECT * FROM t1 JOIN t2 ON t1.id = t1.id")]  // self-comparison: same table.column
    [InlineData("SELECT * FROM t1 a JOIN t2 b ON a.id = a.id")]  // self-comparison with alias
    [InlineData("SELECT * FROM t1 INNER JOIN t2 ON 1=1")]  // INNER JOIN with 1=1
    [InlineData("SELECT * FROM t1 LEFT JOIN t2 ON 1=1")]  // LEFT JOIN with 1=1
    public void Analyze_WhenJoinConditionAlwaysTrue_ReturnsDiagnostic(string sql)
    {
        var rule = new JoinConditionAlwaysTrueRule();
        var context = CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.NotEmpty(diagnostics);
        Assert.Contains(diagnostics, d => d.Data?.RuleId == "semantic/join-condition-always-true");
        Assert.All(diagnostics.Where(d => d.Data?.RuleId == "semantic/join-condition-always-true"), d =>
        {
            Assert.Equal("Correctness", d.Data?.Category);
            Assert.False(d.Data?.Fixable);
        });
    }

    [Theory]
    [InlineData("SELECT * FROM t1 JOIN t2 ON t1.id = t2.id")]  // valid join
    [InlineData("SELECT * FROM t1 JOIN t2 ON t1.status = 1")]  // valid filter (not always true)
    [InlineData("SELECT * FROM t1 a JOIN t2 b ON a.id = b.id")]  // valid join with aliases
    [InlineData("SELECT * FROM t1 JOIN t2 ON t1.a = t2.a AND t1.b = t2.b")]  // composite join
    [InlineData("SELECT * FROM t1 CROSS JOIN t2")]  // CROSS JOIN (no ON clause)
    [InlineData("SELECT * FROM t1, t2")]  // comma join (no ON clause)
    [InlineData("SELECT * FROM t1 JOIN t2 ON t1.id <> t2.id")]  // not-equal join
    public void Analyze_WhenJoinConditionValid_ReturnsEmpty(string sql)
    {
        var rule = new JoinConditionAlwaysTrueRule();
        var context = CreateContext(sql);

        var diagnostics = rule.Analyze(context).Where(d => d.Data?.RuleId == "semantic/join-condition-always-true").ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_MultipleJoinsWithAlwaysTrue_ReportsEach()
    {
        var rule = new JoinConditionAlwaysTrueRule();
        var sql = "SELECT * FROM t1 JOIN t2 ON 1=1 JOIN t3 ON 1=1";
        var context = CreateContext(sql);

        var diagnostics = rule.Analyze(context).Where(d => d.Data?.RuleId == "semantic/join-condition-always-true").ToArray();

        Assert.True(diagnostics.Length >= 2, $"Expected at least 2 diagnostics, got {diagnostics.Length}");
    }

    [Fact]
    public void Analyze_MixedConditions_OnlyReportsAlwaysTrue()
    {
        var rule = new JoinConditionAlwaysTrueRule();
        var sql = "SELECT * FROM t1 JOIN t2 ON 1=1 JOIN t3 ON t1.id = t3.id";
        var context = CreateContext(sql);

        var diagnostics = rule.Analyze(context).Where(d => d.Data?.RuleId == "semantic/join-condition-always-true").ToArray();

        // Only the first JOIN (1=1) should be reported
        Assert.Single(diagnostics);
    }

    [Fact]
    public void Analyze_SelfComparisonDifferentColumns_NoWarning()
    {
        var rule = new JoinConditionAlwaysTrueRule();
        var sql = "SELECT * FROM t1 JOIN t2 ON t1.a = t1.b";  // same table, different columns
        var context = CreateContext(sql);

        var diagnostics = rule.Analyze(context).Where(d => d.Data?.RuleId == "semantic/join-condition-always-true").ToArray();

        // This is unusual but not necessarily "always true" - could be valid business logic
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_EmptyInput_ReturnsEmpty()
    {
        var rule = new JoinConditionAlwaysTrueRule();
        var context = CreateContext("");

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void GetFixes_ReturnsEmpty()
    {
        var rule = new JoinConditionAlwaysTrueRule();
        var context = CreateContext("SELECT * FROM t1 JOIN t2 ON 1=1");
        var diagnostic = new Diagnostic(
            Range: new TsqlRefine.PluginSdk.Range(new Position(0, 0), new Position(0, 10)),
            Message: "test",
            Code: "semantic/join-condition-always-true"
        );

        var fixes = rule.GetFixes(context, diagnostic).ToArray();

        Assert.Empty(fixes);
    }

    [Fact]
    public void Metadata_HasCorrectValues()
    {
        var rule = new JoinConditionAlwaysTrueRule();

        Assert.Equal("semantic/join-condition-always-true", rule.Metadata.RuleId);
        Assert.Equal("Correctness", rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Warning, rule.Metadata.DefaultSeverity);
        Assert.False(rule.Metadata.Fixable);
        Assert.Contains("JOIN", rule.Metadata.Description, StringComparison.OrdinalIgnoreCase);
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
