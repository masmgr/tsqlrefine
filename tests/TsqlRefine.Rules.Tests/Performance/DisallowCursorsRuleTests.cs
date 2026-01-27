using System.IO;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules;
using TsqlRefine.Rules.Rules.Performance;

namespace TsqlRefine.Rules.Tests.Performance;

public sealed class DisallowCursorsRuleTests
{
    [Fact]
    public void Metadata_ShouldHaveCorrectProperties()
    {
        var rule = new DisallowCursorsRule();

        Assert.Equal("disallow-cursors", rule.Metadata.RuleId);
        Assert.Equal("Performance", rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Warning, rule.Metadata.DefaultSeverity);
        Assert.False(rule.Metadata.Fixable);
    }

    [Theory]
    [InlineData("DECLARE cursor_name CURSOR FOR SELECT * FROM users;")]
    [InlineData(@"
        DECLARE @name VARCHAR(50);
        DECLARE cursor_name CURSOR FOR SELECT name FROM users;
        OPEN cursor_name;
        FETCH NEXT FROM cursor_name INTO @name;
        CLOSE cursor_name;
        DEALLOCATE cursor_name;")]
    [InlineData("DECLARE myCursor CURSOR FAST_FORWARD FOR SELECT id FROM products")]
    public void Analyze_WhenCursorDeclared_ReturnsDiagnostic(string sql)
    {
        var rule = new DisallowCursorsRule();
        var context = CreateContext(sql);
        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("disallow-cursors", diagnostics[0].Code);
        Assert.Contains("cursor", diagnostics[0].Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("SELECT * FROM users")]
    [InlineData("INSERT INTO logs (message) VALUES ('test')")]
    [InlineData("-- DECLARE cursor_name CURSOR FOR SELECT * FROM users")]
    [InlineData(@"
        DECLARE @table TABLE (id INT);
        INSERT INTO @table SELECT id FROM users;")]
    public void Analyze_WhenNoCursor_ReturnsNoDiagnostic(string sql)
    {
        var rule = new DisallowCursorsRule();
        var context = CreateContext(sql);
        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_WhenMultipleCursors_ReturnsMultipleDiagnostics()
    {
        var sql = @"
            DECLARE cursor1 CURSOR FOR SELECT id FROM table1;
            DECLARE cursor2 CURSOR FOR SELECT name FROM table2;
            DECLARE cursor3 CURSOR FOR SELECT value FROM table3;
        ";

        var rule = new DisallowCursorsRule();
        var context = CreateContext(sql);
        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Equal(3, diagnostics.Length);
        Assert.All(diagnostics, d => Assert.Equal("disallow-cursors", d.Code));
    }

    [Fact]
    public void GetFixes_ReturnsNoFixes()
    {
        var rule = new DisallowCursorsRule();
        var context = CreateContext("DECLARE cursor_name CURSOR FOR SELECT * FROM users");
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
