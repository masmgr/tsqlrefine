using System.IO;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules;
using Xunit;

namespace TsqlRefine.Rules.Tests;

public sealed class NonSargableRuleTests
{
    private readonly NonSargableRule _rule = new();

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
    public void Analyze_FunctionOnColumnInWhere_ReturnsDiagnostic()
    {
        // Arrange
        var sql = "SELECT * FROM users WHERE LTRIM(username) = 'admin';";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("non-sargable", diagnostic.Code);
        Assert.Contains("LTRIM", diagnostic.Message);
        Assert.Contains("index", diagnostic.Message.ToLowerInvariant());
    }

    [Fact]
    public void Analyze_DatePartInWhere_ReturnsDiagnostic()
    {
        // Arrange
        var sql = "SELECT * FROM orders WHERE YEAR(order_date) = 2023;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("non-sargable", diagnostic.Code);
        Assert.Contains("YEAR", diagnostic.Message);
    }

    [Fact]
    public void Analyze_SubstringInJoinCondition_ReturnsDiagnostic()
    {
        // Arrange
        var sql = @"
            SELECT *
            FROM users u
            INNER JOIN profiles p ON SUBSTRING(u.username, 1, 5) = p.code;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("non-sargable", diagnostic.Code);
        Assert.Contains("SUBSTRING", diagnostic.Message);
    }

    [Fact]
    public void Analyze_UpperLowerInPredicate_NoDiagnostic()
    {
        // Arrange - UPPER/LOWER are handled by upper-lower rule
        var sql = "SELECT * FROM users WHERE UPPER(username) = 'ADMIN';";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_CastConvertInPredicate_NoDiagnostic()
    {
        // Arrange - CAST/CONVERT are handled by avoid-implicit-conversion-in-predicate rule
        var sql = "SELECT * FROM users WHERE CAST(user_id AS VARCHAR) = '123';";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_FunctionOnLiteral_NoDiagnostic()
    {
        // Arrange
        var sql = "SELECT * FROM users WHERE username = LTRIM('  admin  ');";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_FunctionInSelectList_NoDiagnostic()
    {
        // Arrange
        var sql = "SELECT LTRIM(username) FROM users;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_MultipleFunctionsInWhere_ReturnsMultipleDiagnostics()
    {
        // Arrange
        var sql = @"
            SELECT *
            FROM orders
            WHERE YEAR(order_date) = 2023
              AND MONTH(order_date) = 12;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        Assert.Equal(2, diagnostics.Count);
        Assert.All(diagnostics, d => Assert.Equal("non-sargable", d.Code));
    }

    [Fact]
    public void Analyze_FunctionInHavingClause_ReturnsDiagnostic()
    {
        // Arrange
        var sql = @"
            SELECT COUNT(*)
            FROM users
            GROUP BY department
            HAVING LTRIM(department) = 'Sales';";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("non-sargable", diagnostic.Code);
    }

    [Fact]
    public void Analyze_NestedFunctionsOnColumn_ReturnsDiagnostic()
    {
        // Arrange - Only the outer function is reported
        var sql = "SELECT * FROM users WHERE LTRIM(RTRIM(username)) = 'admin';";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("non-sargable", diagnostic.Code);
        Assert.Contains("LTRIM", diagnostic.Message);
    }

    [Fact]
    public void GetFixes_ReturnsNoFixes()
    {
        // Arrange
        var sql = "SELECT * FROM users WHERE LTRIM(username) = 'admin';";
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
        Assert.Equal("non-sargable", _rule.Metadata.RuleId);
        Assert.Equal("Performance", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Warning, _rule.Metadata.DefaultSeverity);
        Assert.False(_rule.Metadata.Fixable);
    }
}
