using System.IO;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules;
using TsqlRefine.Rules.Rules.Style;

namespace TsqlRefine.Rules.Tests.Style;

public sealed class MultiTableAliasRuleTests
{
    private readonly MultiTableAliasRule _rule = new();

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

    [Fact]
    public void Analyze_SingleTable_NoDiagnostics()
    {
        // Arrange
        var sql = "SELECT Id, Name FROM Users;";

        // Act
        var diagnostics = _rule.Analyze(CreateContext(sql)).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_JoinWithQualifiedColumns_NoDiagnostics()
    {
        // Arrange
        var sql = "SELECT u.Id, o.Total FROM Users u JOIN Orders o ON u.Id = o.UserId;";

        // Act
        var diagnostics = _rule.Analyze(CreateContext(sql)).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_JoinWithUnqualifiedColumn_ReturnsDiagnostic()
    {
        // Arrange
        var sql = "SELECT Id FROM Users u JOIN Orders o ON u.Id = o.UserId;";

        // Act
        var diagnostics = _rule.Analyze(CreateContext(sql)).ToArray();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("semantic/multi-table-alias", diagnostic.Code);
        Assert.Contains("qualified", diagnostic.Message);
    }

    [Fact]
    public void Analyze_JoinWithMixedQualification_ReturnsDiagnosticForUnqualified()
    {
        // Arrange
        var sql = "SELECT u.Id, Name FROM Users u JOIN Orders o ON u.Id = o.UserId;";

        // Act
        var diagnostics = _rule.Analyze(CreateContext(sql)).ToArray();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("semantic/multi-table-alias", diagnostic.Code);
        Assert.Contains("Name", diagnostic.Message);
    }

    [Fact]
    public void Analyze_MultipleJoinsWithUnqualifiedColumns_ReturnsMultipleDiagnostics()
    {
        // Arrange
        var sql = "SELECT Id, Name FROM Users u JOIN Orders o ON u.Id = o.UserId JOIN Products p ON o.ProductId = p.Id;";

        // Act
        var diagnostics = _rule.Analyze(CreateContext(sql)).ToArray();

        // Assert
        Assert.Equal(2, diagnostics.Length);
        Assert.All(diagnostics, d => Assert.Equal("semantic/multi-table-alias", d.Code));
    }

    [Fact]
    public void Analyze_SelectStarWithJoin_NoDiagnostics()
    {
        // Arrange - SELECT * is a special case, not a column reference
        var sql = "SELECT * FROM Users u JOIN Orders o ON u.Id = o.UserId;";

        // Act
        var diagnostics = _rule.Analyze(CreateContext(sql)).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_WhereClauseWithUnqualifiedColumn_ReturnsDiagnostic()
    {
        // Arrange
        var sql = "SELECT u.Id FROM Users u JOIN Orders o ON u.Id = o.UserId WHERE Status = 'Active';";

        // Act
        var diagnostics = _rule.Analyze(CreateContext(sql)).ToArray();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("semantic/multi-table-alias", diagnostic.Code);
        Assert.Contains("Status", diagnostic.Message);
    }
}
