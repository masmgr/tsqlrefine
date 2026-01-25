using System.IO;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules;

namespace TsqlRefine.Rules.Tests;

public sealed class DmlWithoutWhereRuleTests
{
    [Theory]
    [InlineData("UPDATE users SET active = 1;")]
    [InlineData("DELETE FROM orders;")]
    [InlineData("UPDATE dbo.products SET price = price * 1.1;")]
    [InlineData("DELETE orders;")]
    [InlineData("update users set active = 1;")]  // lowercase
    [InlineData("delete from orders;")]  // lowercase
    public void Analyze_WhenDmlWithoutWhere_ReturnsDiagnostic(string sql)
    {
        var rule = new DmlWithoutWhereRule();
        var context = CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("dml-without-where", diagnostics[0].Data?.RuleId);
        Assert.Equal("Safety", diagnostics[0].Data?.Category);
        Assert.False(diagnostics[0].Data?.Fixable);
        Assert.Contains("WHERE", diagnostics[0].Message);
    }

    [Theory]
    [InlineData("UPDATE users SET active = 1 WHERE id = 5;")]
    [InlineData("DELETE FROM orders WHERE status = 'cancelled';")]
    [InlineData("UPDATE users SET active = 1 WHERE created_at < '2020-01-01';")]
    [InlineData("DELETE FROM orders WHERE order_date < DATEADD(year, -1, GETDATE());")]
    [InlineData("SELECT * FROM users;")]  // not DML
    [InlineData("INSERT INTO users (name) VALUES ('test');")]  // INSERT is allowed
    public void Analyze_WhenNotViolating_ReturnsEmpty(string sql)
    {
        var rule = new DmlWithoutWhereRule();
        var context = CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_UpdateWithoutWhere_ReportsAtUpdateKeyword()
    {
        var rule = new DmlWithoutWhereRule();
        var sql = "UPDATE users SET active = 1;";
        var context = CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        var diagnostic = diagnostics[0];

        // UPDATE keyword starts at position 0,0 and ends at 0,6 ("UPDATE" is 6 characters)
        Assert.Equal(0, diagnostic.Range.Start.Line);
        Assert.Equal(0, diagnostic.Range.Start.Character);
    }

    [Fact]
    public void Analyze_DeleteWithoutWhere_ReportsAtDeleteKeyword()
    {
        var rule = new DmlWithoutWhereRule();
        var sql = "DELETE FROM orders;";
        var context = CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        var diagnostic = diagnostics[0];

        // DELETE keyword starts at position 0,0
        Assert.Equal(0, diagnostic.Range.Start.Line);
        Assert.Equal(0, diagnostic.Range.Start.Character);
    }

    [Fact]
    public void Analyze_MultipleViolations_ReturnsMultipleDiagnostics()
    {
        var rule = new DmlWithoutWhereRule();
        var sql = @"UPDATE users SET active = 1;
DELETE FROM orders;";
        var context = CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Equal(2, diagnostics.Length);
        Assert.All(diagnostics, d => Assert.Equal("dml-without-where", d.Data?.RuleId));
    }

    [Fact]
    public void Analyze_EmptyInput_ReturnsEmpty()
    {
        var rule = new DmlWithoutWhereRule();
        var context = CreateContext("");

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void GetFixes_ReturnsEmpty()
    {
        var rule = new DmlWithoutWhereRule();
        var context = CreateContext("UPDATE users SET active = 1;");
        var diagnostic = new Diagnostic(
            Range: new TsqlRefine.PluginSdk.Range(new Position(0, 0), new Position(0, 6)),
            Message: "test",
            Code: "dml-without-where"
        );

        var fixes = rule.GetFixes(context, diagnostic).ToArray();

        Assert.Empty(fixes);
    }

    [Fact]
    public void Metadata_HasCorrectValues()
    {
        var rule = new DmlWithoutWhereRule();

        Assert.Equal("dml-without-where", rule.Metadata.RuleId);
        Assert.Equal("Safety", rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Error, rule.Metadata.DefaultSeverity);
        Assert.False(rule.Metadata.Fixable);
        Assert.Contains("UPDATE", rule.Metadata.Description);
        Assert.Contains("DELETE", rule.Metadata.Description);
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
