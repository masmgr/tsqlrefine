using System.IO;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules;

namespace TsqlRefine.Rules.Tests;

public sealed class ObjectPropertyRuleTests
{
    [Fact]
    public void Metadata_ShouldHaveCorrectProperties()
    {
        var rule = new ObjectPropertyRule();

        Assert.Equal("object-property", rule.Metadata.RuleId);
        Assert.Equal("Performance", rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Warning, rule.Metadata.DefaultSeverity);
        Assert.False(rule.Metadata.Fixable);
    }

    [Theory]
    [InlineData("SELECT OBJECTPROPERTY(OBJECT_ID('dbo.Users'), 'TableHasPrimaryKey')")]
    [InlineData("IF OBJECTPROPERTY(OBJECT_ID('MyTable'), 'IsUserTable') = 1 SELECT 1")]
    [InlineData(@"
        DECLARE @prop INT
        SET @prop = OBJECTPROPERTY(OBJECT_ID('dbo.MyProc'), 'IsProcedure')")]
    public void Analyze_WhenObjectPropertyUsed_ReturnsDiagnostic(string sql)
    {
        var rule = new ObjectPropertyRule();
        var context = CreateContext(sql);
        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("object-property", diagnostics[0].Code);
        Assert.Contains("OBJECTPROPERTY", diagnostics[0].Message);
    }

    [Theory]
    [InlineData("SELECT OBJECTPROPERTYEX(OBJECT_ID('dbo.Users'), 'BaseType')")]
    [InlineData("SELECT * FROM sys.objects WHERE type = 'U'")]
    [InlineData("SELECT OBJECT_ID('dbo.Users')")]
    public void Analyze_WhenNoObjectProperty_ReturnsNoDiagnostic(string sql)
    {
        var rule = new ObjectPropertyRule();
        var context = CreateContext(sql);
        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_WhenMultipleObjectProperty_ReturnsMultipleDiagnostics()
    {
        var sql = @"
            SELECT OBJECTPROPERTY(OBJECT_ID('Table1'), 'IsTable'),
                   OBJECTPROPERTY(OBJECT_ID('Table2'), 'IsView')
        ";

        var rule = new ObjectPropertyRule();
        var context = CreateContext(sql);
        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Equal(2, diagnostics.Length);
        Assert.All(diagnostics, d => Assert.Equal("object-property", d.Code));
    }

    [Fact]
    public void GetFixes_ReturnsNoFixes()
    {
        var rule = new ObjectPropertyRule();
        var context = CreateContext("SELECT OBJECTPROPERTY(OBJECT_ID('test'), 'IsTable')");
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
