using System.IO;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules;

namespace TsqlRefine.Rules.Tests;

public sealed class LeftJoinFilteredByWhereRuleTests
{
    [Theory]
    [InlineData("SELECT * FROM t1 LEFT JOIN t2 ON t1.id = t2.id WHERE t2.status = 1")]  // filters right-side
    [InlineData("SELECT * FROM t1 LEFT JOIN t2 ON t1.id = t2.id WHERE t2.id IN (1,2,3)")]  // IN clause on right-side
    [InlineData("SELECT * FROM t1 LEFT JOIN t2 ON t1.id = t2.id WHERE t2.name = 'test'")]  // string comparison
    [InlineData("SELECT * FROM t1 a LEFT JOIN t2 b ON a.id = b.id WHERE b.status = 1")]  // with aliases
    [InlineData("SELECT * FROM t1 LEFT JOIN t2 ON t1.id = t2.id WHERE t2.active = 1")]  // boolean field
    public void Analyze_WhenLeftJoinFilteredByWhere_ReturnsDiagnostic(string sql)
    {
        var rule = new LeftJoinFilteredByWhereRule();
        var context = CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.NotEmpty(diagnostics);
        Assert.Contains(diagnostics, d => d.Data?.RuleId == "semantic/left-join-filtered-by-where");
        Assert.All(diagnostics.Where(d => d.Data?.RuleId == "semantic/left-join-filtered-by-where"), d =>
        {
            Assert.Equal("Correctness", d.Data?.Category);
            Assert.False(d.Data?.Fixable);
            Assert.Contains("LEFT JOIN", d.Message, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Theory]
    [InlineData("SELECT * FROM t1 LEFT JOIN t2 ON t1.id = t2.id WHERE t1.status = 1")]  // filters left side
    [InlineData("SELECT * FROM t1 LEFT JOIN t2 ON t1.id = t2.id WHERE t2.id IS NOT NULL")]  // intentional IS NOT NULL
    [InlineData("SELECT * FROM t1 LEFT JOIN t2 ON t1.id = t2.id WHERE t2.id IS NULL")]  // IS NULL (keeps LEFT JOIN semantic)
    [InlineData("SELECT * FROM t1 INNER JOIN t2 ON t1.id = t2.id WHERE t2.status = 1")]  // INNER JOIN (not LEFT)
    [InlineData("SELECT * FROM t1 LEFT JOIN t2 ON t1.id = t2.id")]  // no WHERE clause
    [InlineData("SELECT * FROM t1 a LEFT JOIN t2 b ON a.id = b.id WHERE a.status = 1")]  // filters left side with alias
    [InlineData("SELECT * FROM t1 a LEFT JOIN t2 b ON a.id = b.id WHERE a.id = 1 AND b.id IS NOT NULL")]  // explicit NULL check
    public void Analyze_WhenLeftJoinNotFilteredByWhere_ReturnsEmpty(string sql)
    {
        var rule = new LeftJoinFilteredByWhereRule();
        var context = CreateContext(sql);

        var diagnostics = rule.Analyze(context).Where(d => d.Data?.RuleId == "semantic/left-join-filtered-by-where").ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_MultipleLeftJoins_OnlyReportsFiltered()
    {
        var rule = new LeftJoinFilteredByWhereRule();
        var sql = "SELECT * FROM t1 LEFT JOIN t2 ON t1.id = t2.id LEFT JOIN t3 ON t1.id = t3.id WHERE t2.status = 1";
        var context = CreateContext(sql);

        var diagnostics = rule.Analyze(context).Where(d => d.Data?.RuleId == "semantic/left-join-filtered-by-where").ToArray();

        // Only t2 is filtered, not t3
        Assert.Single(diagnostics);
        Assert.Contains("t2", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_LeftJoinWithComplexWhere_DetectsFilter()
    {
        var rule = new LeftJoinFilteredByWhereRule();
        var sql = "SELECT * FROM t1 LEFT JOIN t2 ON t1.id = t2.id WHERE t1.active = 1 AND t2.status = 'active'";
        var context = CreateContext(sql);

        var diagnostics = rule.Analyze(context).Where(d => d.Data?.RuleId == "semantic/left-join-filtered-by-where").ToArray();

        Assert.NotEmpty(diagnostics);
    }

    [Fact]
    public void Analyze_EmptyInput_ReturnsEmpty()
    {
        var rule = new LeftJoinFilteredByWhereRule();
        var context = CreateContext("");

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void GetFixes_ReturnsEmpty()
    {
        var rule = new LeftJoinFilteredByWhereRule();
        var context = CreateContext("SELECT * FROM t1 LEFT JOIN t2 ON t1.id = t2.id WHERE t2.status = 1");
        var diagnostic = new Diagnostic(
            Range: new TsqlRefine.PluginSdk.Range(new Position(0, 0), new Position(0, 10)),
            Message: "test",
            Code: "semantic/left-join-filtered-by-where"
        );

        var fixes = rule.GetFixes(context, diagnostic).ToArray();

        Assert.Empty(fixes);
    }

    [Fact]
    public void Metadata_HasCorrectValues()
    {
        var rule = new LeftJoinFilteredByWhereRule();

        Assert.Equal("semantic/left-join-filtered-by-where", rule.Metadata.RuleId);
        Assert.Equal("Correctness", rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Warning, rule.Metadata.DefaultSeverity);
        Assert.False(rule.Metadata.Fixable);
        Assert.Contains("LEFT JOIN", rule.Metadata.Description, StringComparison.OrdinalIgnoreCase);
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
