using System.IO;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules;
using Xunit;

namespace TsqlRefine.Rules.Tests;

public sealed class CountStarRuleTests
{
    private readonly CountStarRule _rule = new();

    private static RuleContext CreateContext(string sql) => new(
        FilePath: "<test>",
        CompatLevel: 150,
        Ast: new ScriptDomAst(sql),
        Tokens: Tokenize(sql),
        Settings: new RuleSettings()
    );

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
    public void Analyze_CountStar_ReturnsDiagnostic()
    {
        // Arrange
        var sql = "SELECT COUNT(*) FROM users;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("count-star", diagnostic.Code);
        Assert.Contains("COUNT(*)", diagnostic.Message);
        Assert.Contains("COUNT(1)", diagnostic.Message);
    }

    [Fact]
    public void Analyze_CountStarWithGroupBy_ReturnsDiagnostic()
    {
        // Arrange
        var sql = @"
            SELECT department, COUNT(*)
            FROM users
            GROUP BY department;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("count-star", diagnostic.Code);
    }

    [Fact]
    public void Analyze_CountColumn_NoDiagnostic()
    {
        // Arrange
        var sql = "SELECT COUNT(user_id) FROM users;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_CountOne_NoDiagnostic()
    {
        // Arrange
        var sql = "SELECT COUNT(1) FROM users;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_CountDistinct_NoDiagnostic()
    {
        // Arrange
        var sql = "SELECT COUNT(DISTINCT user_id) FROM users;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_MultipleCountStar_ReturnsMultipleDiagnostics()
    {
        // Arrange
        var sql = @"
            SELECT COUNT(*) AS total,
                   COUNT(*) OVER (PARTITION BY department) AS dept_count
            FROM users;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        Assert.Equal(2, diagnostics.Count);
        Assert.All(diagnostics, d => Assert.Equal("count-star", d.Code));
    }

    [Fact]
    public void Analyze_CountStarInSubquery_ReturnsDiagnostic()
    {
        // Arrange
        var sql = @"
            SELECT *
            FROM users
            WHERE user_id IN (SELECT COUNT(*) FROM logins);";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("count-star", diagnostic.Code);
    }

    [Fact]
    public void Analyze_OtherAggregatesWithStar_NoDiagnostic()
    {
        // Arrange - Only COUNT(*) should be flagged
        var sql = "SELECT AVG(salary), SUM(hours) FROM employees;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void GetFixes_ReturnsNoFixes()
    {
        // Arrange
        var sql = "SELECT COUNT(*) FROM users;";
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
        Assert.Equal("count-star", _rule.Metadata.RuleId);
        Assert.Equal("Style", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Information, _rule.Metadata.DefaultSeverity);
        Assert.False(_rule.Metadata.Fixable);
    }
}
