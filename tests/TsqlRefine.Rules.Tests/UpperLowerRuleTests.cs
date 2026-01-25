using System.IO;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules;
using Xunit;

namespace TsqlRefine.Rules.Tests;

public sealed class UpperLowerRuleTests
{
    private readonly UpperLowerRule _rule = new();

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
    public void Analyze_UpperInWhereClause_ReturnsDiagnostic()
    {
        // Arrange
        var sql = "SELECT * FROM users WHERE UPPER(username) = 'ADMIN';";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("upper-lower", diagnostic.Code);
        Assert.Contains("UPPER", diagnostic.Message);
        Assert.Contains("index", diagnostic.Message.ToLowerInvariant());
    }

    [Fact]
    public void Analyze_LowerInWhereClause_ReturnsDiagnostic()
    {
        // Arrange
        var sql = "SELECT * FROM users WHERE LOWER(email) = 'test@example.com';";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("upper-lower", diagnostic.Code);
        Assert.Contains("LOWER", diagnostic.Message);
    }

    [Fact]
    public void Analyze_UpperInJoinCondition_ReturnsDiagnostic()
    {
        // Arrange
        var sql = @"
            SELECT *
            FROM users u
            INNER JOIN profiles p ON UPPER(u.username) = UPPER(p.username);";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        Assert.Equal(2, diagnostics.Count);
        Assert.All(diagnostics, d => Assert.Equal("upper-lower", d.Code));
    }

    [Fact]
    public void Analyze_UpperOnLiteral_NoDiagnostic()
    {
        // Arrange - UPPER on literal is acceptable
        var sql = "SELECT * FROM users WHERE username = UPPER('admin');";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_UpperInSelectList_NoDiagnostic()
    {
        // Arrange - UPPER in SELECT list is acceptable
        var sql = "SELECT UPPER(username) FROM users;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_LowerInHavingClause_ReturnsDiagnostic()
    {
        // Arrange
        var sql = @"
            SELECT COUNT(*)
            FROM users
            GROUP BY department
            HAVING LOWER(department) = 'sales';";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("upper-lower", diagnostic.Code);
    }

    [Fact]
    public void Analyze_NoUpperLower_NoDiagnostic()
    {
        // Arrange
        var sql = "SELECT * FROM users WHERE username = 'admin';";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_UpperLowerBothSides_ReturnsDiagnostics()
    {
        // Arrange
        var sql = "SELECT * FROM users WHERE UPPER(username) = UPPER(email);";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        Assert.Equal(2, diagnostics.Count);
        Assert.All(diagnostics, d => Assert.Equal("upper-lower", d.Code));
    }

    [Fact]
    public void GetFixes_ReturnsNoFixes()
    {
        // Arrange
        var sql = "SELECT * FROM users WHERE UPPER(username) = 'ADMIN';";
        var context = CreateContext(sql);
        var diagnostic = _rule.Analyze(context).First();

        // Act
        var fixes = _rule.GetFixes(context, diagnostic);

        // Assert
        Assert.Empty(fixes);
    }

    [Fact]
    public void Metadata_HasCorrectProperties()
    {
        // Assert
        Assert.Equal("upper-lower", _rule.Metadata.RuleId);
        Assert.Equal("Performance", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Warning, _rule.Metadata.DefaultSeverity);
        Assert.False(_rule.Metadata.Fixable);
    }
}
