using System.IO;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules;

namespace TsqlRefine.Rules.Tests;

public sealed class QualifiedSelectColumnsRuleTests
{
    private readonly QualifiedSelectColumnsRule _rule = new();

    [Theory]
    [InlineData("SELECT id FROM users u INNER JOIN orders o ON u.id = o.user_id;")]
    [InlineData("SELECT name FROM users, orders;")]
    [InlineData("SELECT email FROM users u LEFT JOIN orders o ON u.id = o.user_id;")]
    [InlineData("select id from users u, orders o;")]  // lowercase
    public void Analyze_WhenUnqualifiedColumnInMultiTableQuery_ReturnsDiagnostic(string sql)
    {
        // Arrange
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.NotEmpty(diagnostics);
        Assert.All(diagnostics, d => Assert.Equal("qualified-select-columns", d.Code));
    }

    [Theory]
    [InlineData("SELECT u.id FROM users u INNER JOIN orders o ON u.id = o.user_id;")]
    [InlineData("SELECT u.name, o.total FROM users u, orders o;")]
    [InlineData("SELECT users.id FROM users INNER JOIN orders ON users.id = orders.user_id;")]
    [InlineData("SELECT * FROM users;")]  // Single table
    [InlineData("SELECT id, name FROM users;")]  // Single table, unqualified is OK
    [InlineData("")]  // Empty
    public void Analyze_WhenValid_ReturnsEmpty(string sql)
    {
        // Arrange
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_MultipleUnqualifiedColumns_ReturnsMultipleDiagnostics()
    {
        // Arrange
        const string sql = @"
            SELECT id, name, total
            FROM users u
            INNER JOIN orders o ON u.id = o.user_id;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Equal(3, diagnostics.Length);
        Assert.All(diagnostics, d => Assert.Equal("qualified-select-columns", d.Code));
    }

    [Fact]
    public void Analyze_MixedQualifiedAndUnqualified_ReturnsOnlyUnqualified()
    {
        // Arrange
        const string sql = @"
            SELECT u.id, name
            FROM users u
            INNER JOIN orders o ON u.id = o.user_id;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
        Assert.Contains("name", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_SelectStarInMultiTable_ReturnsEmpty()
    {
        // Arrange
        const string sql = "SELECT * FROM users u INNER JOIN orders o ON u.id = o.user_id;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_UnqualifiedInExpression_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = @"
            SELECT UPPER(name) AS upper_name
            FROM users u
            INNER JOIN orders o ON u.id = o.user_id;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.NotEmpty(diagnostics);
        Assert.Contains("name", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_QualifiedInExpression_ReturnsEmpty()
    {
        // Arrange
        const string sql = @"
            SELECT UPPER(u.name) AS upper_name
            FROM users u
            INNER JOIN orders o ON u.id = o.user_id;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_SubqueryWithMultipleTables_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = @"
            SELECT * FROM (
                SELECT id FROM users u, orders o
            ) AS subquery;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.NotEmpty(diagnostics);
        Assert.Equal("qualified-select-columns", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_SingleTableQuery_ReturnsEmpty()
    {
        // Arrange
        const string sql = "SELECT id, name, email FROM users WHERE active = 1;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_EmptyInput_ReturnsEmpty()
    {
        // Arrange
        var context = CreateContext("");

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Metadata_HasCorrectProperties()
    {
        // Assert
        Assert.Equal("qualified-select-columns", _rule.Metadata.RuleId);
        Assert.Equal("Query Structure", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Warning, _rule.Metadata.DefaultSeverity);
        Assert.False(_rule.Metadata.Fixable);
    }

    [Fact]
    public void GetFixes_ReturnsEmpty()
    {
        // Arrange
        var context = CreateContext("SELECT id FROM users u, orders o;");
        var diagnostic = new Diagnostic(
            Range: new TsqlRefine.PluginSdk.Range(new Position(0, 0), new Position(0, 10)),
            Message: "test",
            Code: "qualified-select-columns"
        );

        // Act
        var fixes = _rule.GetFixes(context, diagnostic).ToArray();

        // Assert
        Assert.Empty(fixes);
    }

    private static RuleContext CreateContext(string sql)
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
            CompatLevel: 150,
            Ast: ast,
            Tokens: tokens,
            Settings: new RuleSettings()
        );
    }
}
