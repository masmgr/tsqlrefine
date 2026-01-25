using System.IO;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules;

namespace TsqlRefine.Rules.Tests;

public sealed class ReturnAfterStatementsRuleTests
{
    [Theory]
    [InlineData("BEGIN RETURN; SELECT 1; END")]  // SELECT after RETURN in BEGIN/END
    [InlineData("CREATE PROC p AS BEGIN RETURN; SELECT 1; END")]  // SELECT after RETURN in procedure
    [InlineData("CREATE PROC p AS BEGIN RETURN; UPDATE t SET x=1; END")]  // UPDATE after RETURN
    [InlineData("CREATE PROC p AS BEGIN RETURN; DELETE FROM t; END")]  // DELETE after RETURN
    [InlineData("CREATE PROC p AS BEGIN RETURN 1; PRINT 'unreachable'; END")]  // PRINT after RETURN
    [InlineData("BEGIN RETURN; EXEC sp_test; END")]  // EXEC after RETURN
    public void Analyze_WhenStatementsAfterReturn_ReturnsDiagnostic(string sql)
    {
        var rule = new ReturnAfterStatementsRule();
        var context = CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.NotEmpty(diagnostics);
        Assert.Contains(diagnostics, d => d.Data?.RuleId == "semantic/return-after-statements");
        Assert.All(diagnostics.Where(d => d.Data?.RuleId == "semantic/return-after-statements"), d =>
        {
            Assert.Equal("Correctness", d.Data?.Category);
            Assert.False(d.Data?.Fixable);
            Assert.Contains("unreachable", d.Message, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Theory]
    [InlineData("BEGIN SELECT 1; RETURN; END")]  // RETURN at end
    [InlineData("CREATE PROC p AS BEGIN SELECT 1; RETURN; END")]  // RETURN at end of procedure
    [InlineData("SELECT 1; RETURN;")]  // RETURN after statement (valid)
    [InlineData("CREATE PROC p AS BEGIN IF @x = 1 RETURN; SELECT 1; END")]  // Conditional RETURN, code after is reachable
    [InlineData("BEGIN IF @x = 1 BEGIN RETURN; END; SELECT 1; END")]  // RETURN in nested block, outer code reachable
    [InlineData("CREATE PROC p AS RETURN")]  // Just RETURN, no statements after
    public void Analyze_WhenNoUnreachableStatements_ReturnsEmpty(string sql)
    {
        var rule = new ReturnAfterStatementsRule();
        var context = CreateContext(sql);

        var diagnostics = rule.Analyze(context).Where(d => d.Data?.RuleId == "semantic/return-after-statements").ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_MultipleStatementsAfterReturn_ReportsAll()
    {
        var rule = new ReturnAfterStatementsRule();
        var sql = "BEGIN RETURN; SELECT 1; SELECT 2; UPDATE t SET x=1; END";
        var context = CreateContext(sql);

        var diagnostics = rule.Analyze(context).Where(d => d.Data?.RuleId == "semantic/return-after-statements").ToArray();

        // Should report the first unreachable statement (others are in the same block)
        Assert.NotEmpty(diagnostics);
    }

    [Fact]
    public void Analyze_ReturnInMiddleOfBlock_ReportsSubsequentStatements()
    {
        var rule = new ReturnAfterStatementsRule();
        var sql = "CREATE PROC p AS BEGIN SELECT 1; RETURN; SELECT 2; END";
        var context = CreateContext(sql);

        var diagnostics = rule.Analyze(context).Where(d => d.Data?.RuleId == "semantic/return-after-statements").ToArray();

        Assert.NotEmpty(diagnostics);
    }

    [Fact]
    public void Analyze_EmptyInput_ReturnsEmpty()
    {
        var rule = new ReturnAfterStatementsRule();
        var context = CreateContext("");

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void GetFixes_ReturnsEmpty()
    {
        var rule = new ReturnAfterStatementsRule();
        var context = CreateContext("BEGIN RETURN; SELECT 1; END");
        var diagnostic = new Diagnostic(
            Range: new TsqlRefine.PluginSdk.Range(new Position(0, 0), new Position(0, 10)),
            Message: "test",
            Code: "semantic/return-after-statements"
        );

        var fixes = rule.GetFixes(context, diagnostic).ToArray();

        Assert.Empty(fixes);
    }

    [Fact]
    public void Metadata_HasCorrectValues()
    {
        var rule = new ReturnAfterStatementsRule();

        Assert.Equal("semantic/return-after-statements", rule.Metadata.RuleId);
        Assert.Equal("Correctness", rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Warning, rule.Metadata.DefaultSeverity);
        Assert.False(rule.Metadata.Fixable);
        Assert.Contains("unreachable", rule.Metadata.Description, StringComparison.OrdinalIgnoreCase);
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
