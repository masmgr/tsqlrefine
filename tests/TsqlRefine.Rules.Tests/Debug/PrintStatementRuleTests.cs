using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Debug;

namespace TsqlRefine.Rules.Tests.Debug;

public sealed class PrintStatementRuleTests
{
    [Fact]
    public void Metadata_ShouldHaveCorrectProperties()
    {
        var rule = new PrintStatementRule();

        Assert.Equal("print-statement", rule.Metadata.RuleId);
        Assert.Equal("Debug", rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Information, rule.Metadata.DefaultSeverity);
        Assert.False(rule.Metadata.Fixable);
    }

    [Theory]
    [InlineData("PRINT 'This is a debug message';")]
    [InlineData("PRINT 'Hello World'")]
    [InlineData(@"
        BEGIN
            PRINT 'Log message'
        END")]
    [InlineData("PRINT @Variable")]
    public void Analyze_WhenPrintStatement_ReturnsDiagnostic(string sql)
    {
        var rule = new PrintStatementRule();
        var context = CreateContext(sql);
        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("print-statement", diagnostics[0].Code);
        Assert.Contains("THROW", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_PrintStatement_HighlightsPrintKeyword()
    {
        var sql = "PRINT 'This is a debug message';";
        var rule = new PrintStatementRule();
        var context = CreateContext(sql);
        var diagnostics = rule.Analyze(context).ToArray();

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(0, diagnostic.Range.Start.Line);
        Assert.Equal(0, diagnostic.Range.Start.Character);
        Assert.Equal(0, diagnostic.Range.End.Line);
        Assert.Equal(5, diagnostic.Range.End.Character);
    }

    [Theory]
    [InlineData("SELECT 'Hello World'")]
    [InlineData("RAISERROR('This is an error', 16, 1)")]
    [InlineData("INSERT INTO logs (message) VALUES ('Log message')")]
    [InlineData("-- PRINT is commented out")]
    public void Analyze_WhenNoPrintStatement_ReturnsNoDiagnostic(string sql)
    {
        var rule = new PrintStatementRule();
        var context = CreateContext(sql);
        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_WhenMultiplePrintStatements_ReturnsMultipleDiagnostics()
    {
        var sql = @"
            PRINT 'First message';
            PRINT 'Second message';
            PRINT 'Third message';
        ";

        var rule = new PrintStatementRule();
        var context = CreateContext(sql);
        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Equal(3, diagnostics.Length);
        Assert.All(diagnostics, d => Assert.Equal("print-statement", d.Code));
    }

    [Fact]
    public void GetFixes_ReturnsNoFixes()
    {
        var rule = new PrintStatementRule();
        var context = CreateContext("PRINT 'test'");
        var diagnostic = rule.Analyze(context).First();
        var fixes = rule.GetFixes(context, diagnostic).ToArray();

        Assert.Empty(fixes);
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
