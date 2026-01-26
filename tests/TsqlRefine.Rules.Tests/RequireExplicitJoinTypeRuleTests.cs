using System.IO;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules;

namespace TsqlRefine.Rules.Tests;

public sealed class RequireExplicitJoinTypeRuleTests
{
    private readonly RequireExplicitJoinTypeRule _rule = new();

    [Theory]
    [InlineData("SELECT * FROM users, orders;")]
    [InlineData("SELECT * FROM users, orders, products;")]
    [InlineData("SELECT u.name, o.total FROM users u, orders o;")]
    [InlineData("SELECT * FROM dbo.users, dbo.orders;")]
    [InlineData("select * from users, orders;")]  // lowercase
    public void Analyze_WhenCommaSeparatedTables_ReturnsDiagnostic(string sql)
    {
        // Arrange
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
        Assert.Equal("require-explicit-join-type", diagnostics[0].Code);
        Assert.Contains("explicit JOIN", diagnostics[0].Message);
    }

    [Theory]
    [InlineData("SELECT * FROM users INNER JOIN orders ON users.id = orders.user_id;")]
    [InlineData("SELECT * FROM users LEFT JOIN orders ON users.id = orders.user_id;")]
    [InlineData("SELECT * FROM users RIGHT JOIN orders ON users.id = orders.user_id;")]
    [InlineData("SELECT * FROM users FULL JOIN orders ON users.id = orders.user_id;")]
    [InlineData("SELECT * FROM users FULL OUTER JOIN orders ON users.id = orders.user_id;")]
    [InlineData("SELECT * FROM users CROSS JOIN orders;")]
    [InlineData("SELECT * FROM users;")]  // Single table
    [InlineData("SELECT name, email FROM users;")]
    [InlineData("")]  // Empty
    public void Analyze_WhenExplicitJoin_ReturnsEmpty(string sql)
    {
        // Arrange
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_MultipleCommaSeparatedTables_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = "SELECT * FROM users, orders, products, categories;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
        Assert.Equal("require-explicit-join-type", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_SingleTable_ReturnsEmpty()
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
    public void Analyze_CommaSeparatedWithWhere_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = @"
            SELECT u.name, o.total
            FROM users u, orders o
            WHERE u.id = o.user_id;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
        Assert.Equal("require-explicit-join-type", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_ExplicitJoinChain_ReturnsEmpty()
    {
        // Arrange
        const string sql = @"
            SELECT u.name, o.total, p.name
            FROM users u
            INNER JOIN orders o ON u.id = o.user_id
            INNER JOIN products p ON o.product_id = p.id;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_SubqueryWithCommaSeparated_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = @"
            SELECT * FROM (
                SELECT * FROM users, orders
            ) AS subquery;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.NotEmpty(diagnostics);
        Assert.Equal("require-explicit-join-type", diagnostics[0].Code);
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
        Assert.Equal("require-explicit-join-type", _rule.Metadata.RuleId);
        Assert.Equal("Query Structure", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Warning, _rule.Metadata.DefaultSeverity);
        Assert.False(_rule.Metadata.Fixable);
    }

    [Fact]
    public void GetFixes_ReturnsEmpty()
    {
        // Arrange
        var context = CreateContext("SELECT * FROM users, orders;");
        var diagnostic = new Diagnostic(
            Range: new TsqlRefine.PluginSdk.Range(new Position(0, 0), new Position(0, 10)),
            Message: "test",
            Code: "require-explicit-join-type"
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
