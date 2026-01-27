using System.IO;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules;
using TsqlRefine.Rules.Rules.Naming;

namespace TsqlRefine.Rules.Tests.Naming;

public sealed class MeaningfulAliasRuleTests
{
    private readonly MeaningfulAliasRule _rule = new();

    [Fact]
    public void Analyze_SingleCharAliasInMultiTableQuery_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = "SELECT * FROM users u JOIN orders o ON u.id = o.user_id;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Equal(2, diagnostics.Length); // Both 'u' and 'o' are single-character
        Assert.All(diagnostics, d => Assert.Equal("meaningful-alias", d.Code));
    }

    [Fact]
    public void Analyze_MeaningfulAliasInMultiTableQuery_ReturnsNoDiagnostic()
    {
        // Arrange
        const string sql = "SELECT * FROM users usr JOIN orders ord ON usr.id = ord.user_id;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_SingleCharAliasInSingleTableQuery_ReturnsNoDiagnostic()
    {
        // Arrange - Single-character aliases are acceptable in single-table queries
        const string sql = "SELECT u.id FROM users u;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_MixedAliases_ReturnsOneDiagnostic()
    {
        // Arrange
        const string sql = "SELECT * FROM users usr JOIN orders o ON usr.id = o.user_id;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
        Assert.Equal("meaningful-alias", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_ThreeTablesWithSingleCharAliases_ReturnsThreeDiagnostics()
    {
        // Arrange
        const string sql = @"
            SELECT *
            FROM users u
            JOIN orders o ON u.id = o.user_id
            JOIN products p ON o.product_id = p.id;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Equal(3, diagnostics.Length);
    }

    [Fact]
    public void Analyze_SubqueryWithSingleCharAlias_ReturnsNoDiagnostic()
    {
        // Arrange - Only one table reference at the outer level
        const string sql = "SELECT * FROM (SELECT 1 AS x) s;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_NoAliases_ReturnsNoDiagnostic()
    {
        // Arrange
        const string sql = "SELECT * FROM users JOIN orders ON users.id = orders.user_id;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_EmptyInput_ReturnsNoDiagnostic()
    {
        // Arrange
        const string sql = "";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Metadata_HasCorrectProperties()
    {
        // Assert
        Assert.Equal("meaningful-alias", _rule.Metadata.RuleId);
        Assert.Equal("Style", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Information, _rule.Metadata.DefaultSeverity);
        Assert.False(_rule.Metadata.Fixable);
    }

    private static RuleContext CreateContext(string sql)
    {
        var parser = new TSql150Parser(initialQuotedIdentifiers: true);

        // Parse the fragment
        using var fragmentReader = new StringReader(sql);
        var fragment = parser.Parse(fragmentReader, out IList<ParseError> parseErrors);

        // Parse the tokens
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
