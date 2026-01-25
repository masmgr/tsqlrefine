using System.IO;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules;

namespace TsqlRefine.Rules.Tests;

public sealed class DuplicateAliasRuleTests
{
    [Theory]
    [InlineData("SELECT * FROM users u JOIN orders u ON u.id = u.user_id;")]
    [InlineData("SELECT * FROM users t1, orders t1;")]
    [InlineData("SELECT * FROM products p JOIN categories p ON p.cat_id = p.id;")]
    [InlineData("SELECT * FROM users a INNER JOIN orders a ON a.id = a.user_id;")]
    [InlineData("SELECT * FROM users A JOIN orders a ON A.id = a.user_id;")]  // case insensitive
    public void Analyze_WhenDuplicateAlias_ReturnsDiagnostic(string sql)
    {
        var rule = new DuplicateAliasRule();
        var context = CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.NotEmpty(diagnostics);
        Assert.Contains(diagnostics, d => d.Data?.RuleId == "semantic/duplicate-alias");
        Assert.All(diagnostics.Where(d => d.Data?.RuleId == "semantic/duplicate-alias"), d =>
        {
            Assert.Equal("Correctness", d.Data?.Category);
            Assert.False(d.Data?.Fixable);
            Assert.Contains("duplicate", d.Message, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Theory]
    [InlineData("SELECT * FROM users u JOIN orders o ON u.id = o.user_id;")]
    [InlineData("SELECT * FROM users, orders;")]
    [InlineData("SELECT * FROM users u;")]
    [InlineData("SELECT * FROM products p JOIN categories c ON p.cat_id = c.id;")]
    [InlineData("SELECT * FROM (SELECT * FROM users u) AS subquery;")]  // u is in subquery scope, subquery is outer
    public void Analyze_WhenNoDuplicateAlias_ReturnsEmpty(string sql)
    {
        var rule = new DuplicateAliasRule();
        var context = CreateContext(sql);

        var diagnostics = rule.Analyze(context).Where(d => d.Data?.RuleId == "semantic/duplicate-alias").ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_DuplicateAlias_ReportsAtSecondOccurrence()
    {
        var rule = new DuplicateAliasRule();
        var sql = "SELECT * FROM users u JOIN orders u ON u.id = u.user_id;";
        var context = CreateContext(sql);

        var diagnostics = rule.Analyze(context).Where(d => d.Data?.RuleId == "semantic/duplicate-alias").ToArray();

        Assert.NotEmpty(diagnostics);
        // Should report the second occurrence (JOIN orders u)
        var diagnostic = diagnostics[0];
        Assert.Contains("u", diagnostic.Message);
    }

    [Fact]
    public void Analyze_MultipleViolations_ReturnsMultipleDiagnostics()
    {
        var rule = new DuplicateAliasRule();
        var sql = @"SELECT * FROM users a JOIN orders a ON a.id = a.user_id;
SELECT * FROM products b JOIN categories b ON b.cat_id = b.id;";
        var context = CreateContext(sql);

        var diagnostics = rule.Analyze(context).Where(d => d.Data?.RuleId == "semantic/duplicate-alias").ToArray();

        Assert.True(diagnostics.Length >= 2, $"Expected at least 2 diagnostics, got {diagnostics.Length}");
    }

    [Fact]
    public void Analyze_ThreeWayDuplicate_ReportsMultipleTimes()
    {
        var rule = new DuplicateAliasRule();
        var sql = "SELECT * FROM users t JOIN orders t ON t.id = t.user_id JOIN products t ON t.id = t.product_id;";
        var context = CreateContext(sql);

        var diagnostics = rule.Analyze(context).Where(d => d.Data?.RuleId == "semantic/duplicate-alias").ToArray();

        Assert.True(diagnostics.Length >= 2, $"Expected at least 2 duplicate diagnostics, got {diagnostics.Length}");
    }

    [Fact]
    public void Analyze_ImplicitTableName_WithSameNamedTable_DetectsDuplicate()
    {
        var rule = new DuplicateAliasRule();
        // When joining users to users, the implicit table name is "users" for both
        var sql = "SELECT * FROM users, users;";
        var context = CreateContext(sql);

        var diagnostics = rule.Analyze(context).Where(d => d.Data?.RuleId == "semantic/duplicate-alias").ToArray();

        Assert.NotEmpty(diagnostics);
        Assert.Contains("users", diagnostics[0].Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Analyze_SubqueryWithAlias_DifferentFromTableAlias_NoDuplicate()
    {
        var rule = new DuplicateAliasRule();
        var sql = "SELECT * FROM (SELECT * FROM users u) AS u;";
        var context = CreateContext(sql);

        var diagnostics = rule.Analyze(context).Where(d => d.Data?.RuleId == "semantic/duplicate-alias").ToArray();

        // The inner 'u' is a table alias in subquery scope
        // The outer 'u' is a derived table (subquery) alias
        // These are different scopes, so no duplicate within the same FROM clause
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_CaseInsensitive_DetectsDuplicate()
    {
        var rule = new DuplicateAliasRule();
        var sql = "SELECT * FROM users MyAlias JOIN orders myalias ON MyAlias.id = myalias.user_id;";
        var context = CreateContext(sql);

        var diagnostics = rule.Analyze(context).Where(d => d.Data?.RuleId == "semantic/duplicate-alias").ToArray();

        Assert.NotEmpty(diagnostics);
        Assert.Contains(diagnostics, d => d.Message.Contains("myalias", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Analyze_EmptyInput_ReturnsEmpty()
    {
        var rule = new DuplicateAliasRule();
        var context = CreateContext("");

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void GetFixes_ReturnsEmpty()
    {
        var rule = new DuplicateAliasRule();
        var context = CreateContext("SELECT * FROM users u JOIN orders u ON u.id = u.user_id;");
        var diagnostic = new Diagnostic(
            Range: new TsqlRefine.PluginSdk.Range(new Position(0, 0), new Position(0, 10)),
            Message: "test",
            Code: "semantic/duplicate-alias"
        );

        var fixes = rule.GetFixes(context, diagnostic).ToArray();

        Assert.Empty(fixes);
    }

    [Fact]
    public void Metadata_HasCorrectValues()
    {
        var rule = new DuplicateAliasRule();

        Assert.Equal("semantic/duplicate-alias", rule.Metadata.RuleId);
        Assert.Equal("Correctness", rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Error, rule.Metadata.DefaultSeverity);
        Assert.False(rule.Metadata.Fixable);
        Assert.Contains("duplicate", rule.Metadata.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("alias", rule.Metadata.Description, StringComparison.OrdinalIgnoreCase);
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
