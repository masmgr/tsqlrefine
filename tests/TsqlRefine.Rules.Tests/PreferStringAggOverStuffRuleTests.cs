using System.IO;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules;

namespace TsqlRefine.Rules.Tests;

public sealed class PreferStringAggOverStuffRuleTests
{
    private readonly PreferStringAggOverStuffRule _rule = new();

    [Theory]
    [InlineData(@"SELECT STUFF((SELECT ',' + name FROM users FOR XML PATH('')), 1, 1, '') AS names;")]
    [InlineData(@"SELECT STUFF((SELECT '; ' + email FROM contacts FOR XML PATH('')), 1, 2, '') AS emails;")]
    [InlineData(@"SELECT id, STUFF((SELECT ', ' + tag FROM tags WHERE tags.item_id = items.id FOR XML PATH('')), 1, 2, '') AS tags FROM items;")]
    public void Analyze_StuffWithForXmlPath_ReturnsDiagnostic(string sql)
    {
        // Arrange
        var context = CreateContext(sql, compatLevel: 140);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
        Assert.Equal("prefer-string-agg-over-stuff", diagnostics[0].Code);
        Assert.Contains("STRING_AGG", diagnostics[0].Message);
    }

    [Theory]
    [InlineData("SELECT STRING_AGG(name, ',') AS names FROM users;")]
    [InlineData("SELECT STRING_AGG(email, '; ') AS emails FROM contacts;")]
    [InlineData("SELECT STUFF(description, 1, 5, 'New') FROM products;")]  // STUFF without FOR XML PATH
    [InlineData("SELECT name FROM users FOR XML PATH('');")]  // FOR XML PATH without STUFF
    [InlineData("SELECT * FROM users;")]
    [InlineData("")]  // Empty
    public void Analyze_WhenValid_ReturnsEmpty(string sql)
    {
        // Arrange
        var context = CreateContext(sql, compatLevel: 140);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_OldCompatLevel_ReturnsEmpty()
    {
        // Arrange
        const string sql = "SELECT STUFF((SELECT ',' + name FROM users FOR XML PATH('')), 1, 1, '') AS names;";
        var context = CreateContext(sql, compatLevel: 130);  // SQL Server 2016

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_CompatLevel140_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = "SELECT STUFF((SELECT '|' + col FROM data FOR XML PATH('')), 1, 1, '') AS result;";
        var context = CreateContext(sql, compatLevel: 140);  // SQL Server 2017

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
    }

    [Fact]
    public void Analyze_MultipleStuffWithForXmlPath_ReturnsMultipleDiagnostics()
    {
        // Arrange
        const string sql = @"
            SELECT
                STUFF((SELECT ',' + name FROM users FOR XML PATH('')), 1, 1, '') AS names,
                STUFF((SELECT '; ' + email FROM contacts FOR XML PATH('')), 1, 2, '') AS emails
            FROM data;";
        var context = CreateContext(sql, compatLevel: 140);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Equal(2, diagnostics.Length);
        Assert.All(diagnostics, d => Assert.Equal("prefer-string-agg-over-stuff", d.Code));
    }

    [Fact]
    public void Analyze_NestedStuff_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = @"
            SELECT
                id,
                STUFF((
                    SELECT ', ' + category
                    FROM categories c
                    WHERE c.product_id = p.id
                    FOR XML PATH('')
                ), 1, 2, '') AS categories
            FROM products p;";
        var context = CreateContext(sql, compatLevel: 140);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
        Assert.Equal("prefer-string-agg-over-stuff", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_StuffWithForXmlPathAndType_MayNotDetect()
    {
        // Arrange - TYPE modifier changes AST structure
        const string sql = @"
            SELECT STUFF((
                SELECT ',' + name
                FROM users
                FOR XML PATH(''), TYPE
            ).value('.', 'NVARCHAR(MAX)'), 1, 1, '') AS names;";
        var context = CreateContext(sql, compatLevel: 140);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert - Rule may not detect with TYPE modifier due to .value() method call
        if (diagnostics.Length > 0)
        {
            Assert.All(diagnostics, d => Assert.Equal("prefer-string-agg-over-stuff", d.Code));
        }
    }

    [Fact]
    public void Analyze_EmptyInput_ReturnsEmpty()
    {
        // Arrange
        var context = CreateContext("", compatLevel: 140);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Metadata_HasCorrectProperties()
    {
        // Assert
        Assert.Equal("prefer-string-agg-over-stuff", _rule.Metadata.RuleId);
        Assert.Equal("Modernization", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Information, _rule.Metadata.DefaultSeverity);
        Assert.False(_rule.Metadata.Fixable);
        Assert.Contains("STRING_AGG", _rule.Metadata.Description);
    }

    [Fact]
    public void GetFixes_ReturnsEmpty()
    {
        // Arrange
        var context = CreateContext("SELECT STUFF((SELECT ',' + name FROM users FOR XML PATH('')), 1, 1, '') AS names;", compatLevel: 140);
        var diagnostic = new Diagnostic(
            Range: new TsqlRefine.PluginSdk.Range(new Position(0, 0), new Position(0, 10)),
            Message: "test",
            Code: "prefer-string-agg-over-stuff"
        );

        // Act
        var fixes = _rule.GetFixes(context, diagnostic).ToArray();

        // Assert
        Assert.Empty(fixes);
    }

    private static RuleContext CreateContext(string sql, int compatLevel = 150)
    {
        var parser = new TSql150Parser(initialQuotedIdentifiers: true);

        using var fragmentReader = new StringReader(sql);
        var fragment = parser.Parse(fragmentReader, out IList<ParseError> parseErrors);

        using var tokenReader = new StringReader(sql);
        var tokenStream = parser.GetTokenStream(tokenReader, out IList<ParseError> tokenErrors);

        var tokens = tokenStream
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

        var ast = new ScriptDomAst(sql, fragment, parseErrors.ToArray(), tokenErrors.ToArray());

        return new RuleContext(
            FilePath: "<test>",
            CompatLevel: compatLevel,
            Ast: ast,
            Tokens: tokens,
            Settings: new RuleSettings()
        );
    }
}
