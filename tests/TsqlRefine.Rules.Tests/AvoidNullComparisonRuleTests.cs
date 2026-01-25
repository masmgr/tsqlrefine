using System.IO;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules;

namespace TsqlRefine.Rules.Tests;

public sealed class AvoidNullComparisonRuleTests
{
    [Theory]
    [InlineData("SELECT * FROM users WHERE name = NULL;")]
    [InlineData("SELECT * FROM users WHERE status <> NULL;")]
    [InlineData("SELECT * FROM users WHERE email != NULL;")]
    [InlineData("UPDATE users SET active = 0 WHERE last_login = NULL;")]
    [InlineData("DELETE FROM sessions WHERE user_id <> NULL;")]
    [InlineData("SELECT * FROM users WHERE NULL = name;")]  // NULL on left side
    [InlineData("SELECT * FROM users WHERE NULL <> status;")]  // NULL on left side
    [InlineData("select * from users where name = null;")]  // lowercase
    public void Analyze_WhenNullComparison_ReturnsDiagnostic(string sql)
    {
        var rule = new AvoidNullComparisonRule();
        var context = CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("avoid-null-comparison", diagnostics[0].Data?.RuleId);
        Assert.Equal("Correctness", diagnostics[0].Data?.Category);
        Assert.False(diagnostics[0].Data?.Fixable);
        Assert.Contains("NULL", diagnostics[0].Message);
    }

    [Theory]
    [InlineData("SELECT * FROM users WHERE name IS NULL;")]
    [InlineData("SELECT * FROM users WHERE status IS NOT NULL;")]
    [InlineData("SELECT * FROM users WHERE email IS NOT NULL;")]
    [InlineData("SELECT * FROM users WHERE name = 'value';")]
    [InlineData("SELECT * FROM users WHERE id = 123;")]
    [InlineData("SELECT * FROM users WHERE created_at > GETDATE();")]
    [InlineData("SELECT * FROM users WHERE status <> 'active';")]
    [InlineData("SELECT * FROM users WHERE price != 0;")]
    [InlineData("SELECT * FROM users;")]  // No WHERE clause
    public void Analyze_WhenNotViolating_ReturnsEmpty(string sql)
    {
        var rule = new AvoidNullComparisonRule();
        var context = CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_EqualNullComparison_ReportsAtComparisonOperator()
    {
        var rule = new AvoidNullComparisonRule();
        var sql = "SELECT * FROM users WHERE name = NULL;";
        var context = CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        var diagnostic = diagnostics[0];

        // The comparison expression should start at "name"
        Assert.Equal(0, diagnostic.Range.Start.Line);
        Assert.True(diagnostic.Range.Start.Character >= 26); // After "WHERE "
    }

    [Fact]
    public void Analyze_NotEqualNullComparison_ReturnsDiagnostic()
    {
        var rule = new AvoidNullComparisonRule();
        var sql = "SELECT * FROM users WHERE status <> NULL;";
        var context = CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("avoid-null-comparison", diagnostics[0].Data?.RuleId);
    }

    [Fact]
    public void Analyze_NotEqualExclamationNullComparison_ReturnsDiagnostic()
    {
        var rule = new AvoidNullComparisonRule();
        var sql = "SELECT * FROM users WHERE email != NULL;";
        var context = CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("avoid-null-comparison", diagnostics[0].Data?.RuleId);
    }

    [Fact]
    public void Analyze_MultipleViolations_ReturnsMultipleDiagnostics()
    {
        var rule = new AvoidNullComparisonRule();
        var sql = @"SELECT * FROM users WHERE name = NULL AND status <> NULL;
UPDATE users SET active = 0 WHERE last_login = NULL;";
        var context = CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Equal(3, diagnostics.Length);
        Assert.All(diagnostics, d => Assert.Equal("avoid-null-comparison", d.Data?.RuleId));
    }

    [Fact]
    public void Analyze_EmptyInput_ReturnsEmpty()
    {
        var rule = new AvoidNullComparisonRule();
        var context = CreateContext("");

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void GetFixes_ReturnsEmpty()
    {
        var rule = new AvoidNullComparisonRule();
        var context = CreateContext("SELECT * FROM users WHERE name = NULL;");
        var diagnostic = new Diagnostic(
            Range: new TsqlRefine.PluginSdk.Range(new Position(0, 0), new Position(0, 6)),
            Message: "test",
            Code: "avoid-null-comparison"
        );

        var fixes = rule.GetFixes(context, diagnostic).ToArray();

        Assert.Empty(fixes);
    }

    [Fact]
    public void Metadata_HasCorrectValues()
    {
        var rule = new AvoidNullComparisonRule();

        Assert.Equal("avoid-null-comparison", rule.Metadata.RuleId);
        Assert.Equal("Correctness", rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Warning, rule.Metadata.DefaultSeverity);
        Assert.False(rule.Metadata.Fixable);
        Assert.Contains("NULL", rule.Metadata.Description);
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
