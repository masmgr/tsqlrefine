using System.IO;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules;
using TsqlRefine.Rules.Rules.Style;

namespace TsqlRefine.Rules.Tests.Style;

public sealed class PreferTrimOverLtrimRtrimRuleTests
{
    private readonly PreferTrimOverLtrimRtrimRule _rule = new();

    [Theory]
    [InlineData("SELECT LTRIM(RTRIM(name)) FROM users;")]
    [InlineData("SELECT RTRIM(LTRIM(name)) FROM users;")]
    [InlineData("SELECT LTRIM(RTRIM(@value)) AS cleaned;")]
    [InlineData("SELECT id, LTRIM(RTRIM(description)) AS [desc] FROM products;")]
    [InlineData("select ltrim(rtrim(name)) from users;")]  // lowercase
    public void Analyze_WhenNestedLtrimRtrim_ReturnsDiagnostic(string sql)
    {
        // Arrange
        var context = CreateContext(sql, compatLevel: 140);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
        Assert.Equal("prefer-trim-over-ltrim-rtrim", diagnostics[0].Code);
        Assert.Contains("TRIM", diagnostics[0].Message);
    }

    [Theory]
    [InlineData("SELECT TRIM(name) FROM users;")]
    [InlineData("SELECT LTRIM(name) FROM users;")]
    [InlineData("SELECT RTRIM(name) FROM users;")]
    [InlineData("SELECT LTRIM(description) FROM products;")]
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
        const string sql = "SELECT LTRIM(RTRIM(name)) FROM users;";
        var context = CreateContext(sql, compatLevel: 130);  // SQL Server 2016

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_MultipleNestedLtrimRtrim_ReturnsMultipleDiagnostics()
    {
        // Arrange
        const string sql = @"
            SELECT
                LTRIM(RTRIM(first_name)) AS fname,
                RTRIM(LTRIM(last_name)) AS lname
            FROM users;";
        var context = CreateContext(sql, compatLevel: 140);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Equal(2, diagnostics.Length);
        Assert.All(diagnostics, d => Assert.Equal("prefer-trim-over-ltrim-rtrim", d.Code));
    }

    [Fact]
    public void Analyze_NestedInExpression_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = @"
            SELECT name
            FROM users
            WHERE UPPER(LTRIM(RTRIM(email))) = 'TEST@EXAMPLE.COM';";
        var context = CreateContext(sql, compatLevel: 140);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
        Assert.Equal("prefer-trim-over-ltrim-rtrim", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_MixedCases_ReturnsOnlyNested()
    {
        // Arrange
        const string sql = @"
            SELECT
                TRIM(col1),
                LTRIM(RTRIM(col2)),
                LTRIM(col3)
            FROM users;";
        var context = CreateContext(sql, compatLevel: 140);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
        Assert.Contains("col2", sql.Substring(
            diagnostics[0].Range.Start.Character,
            Math.Min(100, sql.Length - diagnostics[0].Range.Start.Character)
        ));
    }

    [Fact]
    public void Analyze_CompatLevel100_ReturnsEmpty()
    {
        // Arrange
        const string sql = "SELECT LTRIM(RTRIM(name)) FROM users;";
        var context = CreateContext(sql, compatLevel: 100);  // SQL Server 2008

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_CompatLevel110_ReturnsEmpty()
    {
        // Arrange
        const string sql = "SELECT LTRIM(RTRIM(name)) FROM users;";
        var context = CreateContext(sql, compatLevel: 110);  // SQL Server 2012

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
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
        Assert.Equal("prefer-trim-over-ltrim-rtrim", _rule.Metadata.RuleId);
        Assert.Equal("Style", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Information, _rule.Metadata.DefaultSeverity);
        Assert.False(_rule.Metadata.Fixable);
        Assert.Contains("SQL Server 2017", _rule.Metadata.Description);
    }

    [Fact]
    public void GetFixes_ReturnsEmpty()
    {
        // Arrange
        var context = CreateContext("SELECT LTRIM(RTRIM(name)) FROM users;", compatLevel: 140);
        var diagnostic = new Diagnostic(
            Range: new TsqlRefine.PluginSdk.Range(new Position(0, 0), new Position(0, 10)),
            Message: "test",
            Code: "prefer-trim-over-ltrim-rtrim"
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
