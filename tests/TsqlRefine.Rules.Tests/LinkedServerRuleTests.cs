using System.IO;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules;

namespace TsqlRefine.Rules.Tests;

public sealed class LinkedServerRuleTests
{
    [Fact]
    public void Metadata_ShouldHaveCorrectProperties()
    {
        var rule = new LinkedServerRule();

        Assert.Equal("linked-server", rule.Metadata.RuleId);
        Assert.Equal("Performance", rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Warning, rule.Metadata.DefaultSeverity);
        Assert.False(rule.Metadata.Fixable);
    }

    [Theory]
    [InlineData("SELECT * FROM [RemoteServer].[RemoteDB].[dbo].[Users]")]
    [InlineData("SELECT * FROM RemoteServer.RemoteDB.dbo.Users")]
    [InlineData("INSERT INTO [Server1].[DB1].[dbo].[Table1] SELECT * FROM [Server2].[DB2].[dbo].[Table2]")]
    public void Analyze_WhenFourPartIdentifier_ReturnsDiagnostic(string sql)
    {
        var rule = new LinkedServerRule();
        var context = CreateContext(sql);
        var diagnostics = rule.Analyze(context).ToArray();

        Assert.NotEmpty(diagnostics);
        Assert.All(diagnostics, d => Assert.Equal("linked-server", d.Code));
        Assert.All(diagnostics, d => Assert.Contains("linked server", d.Message, StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("SELECT * FROM dbo.Users")]
    [InlineData("SELECT * FROM MyDB.dbo.Users")]
    [InlineData("SELECT * FROM [MyDB].[dbo].[Users]")]
    [InlineData("SELECT * FROM sys.tables")]
    public void Analyze_WhenNoLinkedServer_ReturnsNoDiagnostic(string sql)
    {
        var rule = new LinkedServerRule();
        var context = CreateContext(sql);
        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_WhenMultipleLinkedServerReferences_ReturnsMultipleDiagnostics()
    {
        var sql = @"
            SELECT a.*, b.*
            FROM [Server1].[DB1].[dbo].[Table1] a
            JOIN [Server2].[DB2].[dbo].[Table2] b ON a.Id = b.Id
        ";

        var rule = new LinkedServerRule();
        var context = CreateContext(sql);
        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Equal(2, diagnostics.Length);
        Assert.All(diagnostics, d => Assert.Equal("linked-server", d.Code));
    }

    [Fact]
    public void GetFixes_ReturnsNoFixes()
    {
        var rule = new LinkedServerRule();
        var context = CreateContext("SELECT * FROM [Server1].[DB1].[dbo].[Users]");
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
                    text.Length);
            })
            .ToArray();
    }
}
