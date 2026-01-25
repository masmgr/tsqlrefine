using System.IO;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules;
using Xunit;

namespace TsqlRefine.Rules.Tests;

public sealed class JoinKeywordRuleTests
{
    private readonly JoinKeywordRule _rule = new();

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
    public void Analyze_CommaJoin_ReturnsDiagnostic()
    {
        // Arrange
        var sql = "SELECT * FROM users, profiles;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("join-keyword", diagnostic.Code);
        Assert.Contains("implicit join", diagnostic.Message.ToLowerInvariant());
        Assert.Contains("INNER JOIN", diagnostic.Message);
    }

    [Fact]
    public void Analyze_MultipleCommaJoins_ReturnsDiagnostics()
    {
        // Arrange - Each comma is reported separately
        var sql = "SELECT * FROM users, profiles, departments;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        Assert.Equal(2, diagnostics.Count);
        Assert.All(diagnostics, d => Assert.Equal("join-keyword", d.Code));
    }

    [Fact]
    public void Analyze_InnerJoin_NoDiagnostic()
    {
        // Arrange
        var sql = "SELECT * FROM users INNER JOIN profiles ON users.id = profiles.user_id;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_LeftJoin_NoDiagnostic()
    {
        // Arrange
        var sql = "SELECT * FROM users LEFT JOIN profiles ON users.id = profiles.user_id;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_SingleTable_NoDiagnostic()
    {
        // Arrange
        var sql = "SELECT * FROM users;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_CrossJoin_NoDiagnostic()
    {
        // Arrange - CROSS JOIN is explicit
        var sql = "SELECT * FROM users CROSS JOIN profiles;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_CommaJoinInSubquery_ReturnsDiagnostic()
    {
        // Arrange
        var sql = @"
            SELECT *
            FROM (
                SELECT * FROM users, profiles
            ) AS subquery;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("join-keyword", diagnostic.Code);
    }

    [Fact]
    public void Analyze_CommaJoinWithWhere_ReturnsDiagnostic()
    {
        // Arrange
        var sql = "SELECT * FROM users, profiles WHERE users.id = profiles.user_id;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("join-keyword", diagnostic.Code);
    }

    [Fact]
    public void Analyze_MixedJoinTypes_ReturnsDiagnostic()
    {
        // Arrange
        var sql = @"
            SELECT *
            FROM users, profiles
            INNER JOIN departments ON users.dept_id = departments.id;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("join-keyword", diagnostic.Code);
    }

    [Fact]
    public void Analyze_MultipleInnerJoins_NoDiagnostic()
    {
        // Arrange
        var sql = @"
            SELECT *
            FROM users
            INNER JOIN profiles ON users.id = profiles.user_id
            INNER JOIN departments ON users.dept_id = departments.id;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_CommaInCTE_ReturnsDiagnostic()
    {
        // Arrange
        var sql = @"
            WITH cte AS (
                SELECT * FROM users, profiles
            )
            SELECT * FROM cte;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("join-keyword", diagnostic.Code);
    }

    [Fact]
    public void GetFixes_ReturnsNoFixes()
    {
        // Arrange
        var sql = "SELECT * FROM users, profiles;";
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
        Assert.Equal("join-keyword", _rule.Metadata.RuleId);
        Assert.Equal("Style", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Warning, _rule.Metadata.DefaultSeverity);
        Assert.False(_rule.Metadata.Fixable);
    }
}
