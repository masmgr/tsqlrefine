using System.IO;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules;
using TsqlRefine.Rules.Rules.Performance;

namespace TsqlRefine.Rules.Tests.Performance;

public sealed class InformationSchemaRuleTests
{
    [Fact]
    public void Metadata_ShouldHaveCorrectProperties()
    {
        var rule = new InformationSchemaRule();

        Assert.Equal("information-schema", rule.Metadata.RuleId);
        Assert.Equal("Performance", rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Warning, rule.Metadata.DefaultSeverity);
        Assert.False(rule.Metadata.Fixable);
    }

    [Theory]
    [InlineData("SELECT * FROM INFORMATION_SCHEMA.TABLES")]
    [InlineData("SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Users'")]
    [InlineData("SELECT * FROM INFORMATION_SCHEMA.VIEWS")]
    [InlineData("SELECT COUNT(*) FROM INFORMATION_SCHEMA.ROUTINES")]
    public void Analyze_WhenInformationSchemaUsed_ReturnsDiagnostic(string sql)
    {
        var rule = new InformationSchemaRule();
        var context = CreateContext(sql);
        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("information-schema", diagnostics[0].Code);
        Assert.Contains("INFORMATION_SCHEMA", diagnostics[0].Message);
    }

    [Theory]
    [InlineData("SELECT * FROM sys.tables")]
    [InlineData("SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Users')")]
    [InlineData("SELECT * FROM dbo.Users")]
    [InlineData("SELECT * FROM master.dbo.sysprocesses")]
    public void Analyze_WhenNoInformationSchema_ReturnsNoDiagnostic(string sql)
    {
        var rule = new InformationSchemaRule();
        var context = CreateContext(sql);
        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_WhenMultipleInformationSchemaReferences_ReturnsMultipleDiagnostics()
    {
        var sql = @"
            SELECT * FROM INFORMATION_SCHEMA.TABLES
            UNION ALL
            SELECT * FROM INFORMATION_SCHEMA.VIEWS
        ";

        var rule = new InformationSchemaRule();
        var context = CreateContext(sql);
        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Equal(2, diagnostics.Length);
        Assert.All(diagnostics, d => Assert.Equal("information-schema", d.Code));
    }

    [Fact]
    public void GetFixes_ReturnsNoFixes()
    {
        var rule = new InformationSchemaRule();
        var context = CreateContext("SELECT * FROM INFORMATION_SCHEMA.TABLES");
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
