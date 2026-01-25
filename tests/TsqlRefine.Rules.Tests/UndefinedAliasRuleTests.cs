using System.IO;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules;

namespace TsqlRefine.Rules.Tests;

public sealed class UndefinedAliasRuleTests
{
    [Theory]
    [InlineData("SELECT u.id FROM users WHERE x.active = 1;")]
    [InlineData("SELECT t.name FROM users;")]
    [InlineData("SELECT u.id FROM users WHERE u.id = v.id;")]
    [InlineData("SELECT a.id, b.name FROM users WHERE c.active = 1;")]
    public void Analyze_WhenUndefinedAlias_ReturnsDiagnostic(string sql)
    {
        var rule = new UndefinedAliasRule();
        var context = CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.NotEmpty(diagnostics);
        Assert.Contains(diagnostics, d => d.Data?.RuleId == "semantic/undefined-alias");
        Assert.All(diagnostics.Where(d => d.Data?.RuleId == "semantic/undefined-alias"), d =>
        {
            Assert.Equal("Correctness", d.Data?.Category);
            Assert.False(d.Data?.Fixable);
            Assert.Contains("undefined", d.Message, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Theory]
    [InlineData("SELECT u.id FROM users u WHERE u.active = 1;")]
    [InlineData("SELECT users.id FROM users;")]  // implicit table name
    [InlineData("SELECT * FROM users;")]  // no qualified references
    [InlineData("SELECT u.id, o.order_id FROM users u JOIN orders o ON u.id = o.user_id;")]
    [InlineData("SELECT u.id FROM users u;")]
    [InlineData("SELECT id FROM users;")]  // unqualified column
    public void Analyze_WhenNotViolating_ReturnsEmpty(string sql)
    {
        var rule = new UndefinedAliasRule();
        var context = CreateContext(sql);

        var diagnostics = rule.Analyze(context).Where(d => d.Data?.RuleId == "semantic/undefined-alias").ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_UndefinedAlias_ReportsAtColumnReference()
    {
        var rule = new UndefinedAliasRule();
        var sql = "SELECT x.id FROM users u;";
        var context = CreateContext(sql);

        var diagnostics = rule.Analyze(context).Where(d => d.Data?.RuleId == "semantic/undefined-alias").ToArray();

        Assert.Single(diagnostics);
        var diagnostic = diagnostics[0];
        Assert.Contains("x", diagnostic.Message);
    }

    [Fact]
    public void Analyze_MultipleUndefinedAliases_ReturnsMultipleDiagnostics()
    {
        var rule = new UndefinedAliasRule();
        var sql = "SELECT a.id, b.name FROM users u WHERE c.active = 1;";
        var context = CreateContext(sql);

        var diagnostics = rule.Analyze(context).Where(d => d.Data?.RuleId == "semantic/undefined-alias").ToArray();

        Assert.Equal(3, diagnostics.Length);
        Assert.Contains(diagnostics, d => d.Message.Contains("a"));
        Assert.Contains(diagnostics, d => d.Message.Contains("b"));
        Assert.Contains(diagnostics, d => d.Message.Contains("c"));
    }

    [Fact]
    public void Analyze_CaseInsensitive_RecognizesAlias()
    {
        var rule = new UndefinedAliasRule();
        var sql = "SELECT MyAlias.id FROM users myalias;";
        var context = CreateContext(sql);

        var diagnostics = rule.Analyze(context).Where(d => d.Data?.RuleId == "semantic/undefined-alias").ToArray();

        // Should not report error because MyAlias matches myalias (case-insensitive)
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_WithTableSchema_RecognizesImplicitName()
    {
        var rule = new UndefinedAliasRule();
        var sql = "SELECT users.id FROM dbo.users;";
        var context = CreateContext(sql);

        var diagnostics = rule.Analyze(context).Where(d => d.Data?.RuleId == "semantic/undefined-alias").ToArray();

        // Should recognize 'users' as the implicit table name
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_WithExplicitAlias_IgnoresTableName()
    {
        var rule = new UndefinedAliasRule();
        var sql = "SELECT users.id FROM users u;";
        var context = CreateContext(sql);

        var diagnostics = rule.Analyze(context).Where(d => d.Data?.RuleId == "semantic/undefined-alias").ToArray();

        // When alias 'u' is provided, 'users' is not a valid qualifier
        Assert.Single(diagnostics);
        Assert.Contains("users", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_MultipleQueriesInBatch_ValidatesEachIndependently()
    {
        var rule = new UndefinedAliasRule();
        var sql = @"SELECT u.id FROM users u;
SELECT x.id FROM orders o;";
        var context = CreateContext(sql);

        var diagnostics = rule.Analyze(context).Where(d => d.Data?.RuleId == "semantic/undefined-alias").ToArray();

        // Only the second query should have an error (x is undefined)
        Assert.Single(diagnostics);
        Assert.Contains("x", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_SubqueryReferences_ValidatedIndependently()
    {
        var rule = new UndefinedAliasRule();
        // Outer query references 'u', which is defined in outer scope
        // Inner subquery references 'u', which is defined in inner scope
        // This is valid (each SELECT has its own scope)
        var sql = "SELECT u.id FROM (SELECT id FROM users u) AS u;";
        var context = CreateContext(sql);

        var diagnostics = rule.Analyze(context).Where(d => d.Data?.RuleId == "semantic/undefined-alias").ToArray();

        // Inner 'u' is a table alias in subquery
        // Outer 'u' is the subquery alias (derived table)
        // Both are valid in their respective scopes
        // With simple MVP approach, we validate each SELECT independently
        // Outer SELECT sees subquery alias 'u' - this is NOT a column reference, so no diagnostic
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_JoinConditionReferences_Validated()
    {
        var rule = new UndefinedAliasRule();
        var sql = "SELECT * FROM users u JOIN orders o ON x.id = o.user_id;";
        var context = CreateContext(sql);

        var diagnostics = rule.Analyze(context).Where(d => d.Data?.RuleId == "semantic/undefined-alias").ToArray();

        Assert.Single(diagnostics);
        Assert.Contains("x", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_OrderByClauseReferences_Validated()
    {
        var rule = new UndefinedAliasRule();
        var sql = "SELECT u.id FROM users u ORDER BY x.created_at;";
        var context = CreateContext(sql);

        var diagnostics = rule.Analyze(context).Where(d => d.Data?.RuleId == "semantic/undefined-alias").ToArray();

        Assert.Single(diagnostics);
        Assert.Contains("x", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_EmptyInput_ReturnsEmpty()
    {
        var rule = new UndefinedAliasRule();
        var context = CreateContext("");

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void GetFixes_ReturnsEmpty()
    {
        var rule = new UndefinedAliasRule();
        var context = CreateContext("SELECT x.id FROM users u;");
        var diagnostic = new Diagnostic(
            Range: new TsqlRefine.PluginSdk.Range(new Position(0, 7), new Position(0, 11)),
            Message: "test",
            Code: "semantic/undefined-alias"
        );

        var fixes = rule.GetFixes(context, diagnostic).ToArray();

        Assert.Empty(fixes);
    }

    [Fact]
    public void Metadata_HasCorrectValues()
    {
        var rule = new UndefinedAliasRule();

        Assert.Equal("semantic/undefined-alias", rule.Metadata.RuleId);
        Assert.Equal("Correctness", rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Error, rule.Metadata.DefaultSeverity);
        Assert.False(rule.Metadata.Fixable);
        Assert.Contains("undefined", rule.Metadata.Description, StringComparison.OrdinalIgnoreCase);
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
                    text.Length,
                    token.TokenType.ToString());
            })
            .ToArray();
    }
}
