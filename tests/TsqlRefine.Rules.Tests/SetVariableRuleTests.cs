using System.IO;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules;

namespace TsqlRefine.Rules.Tests;

public sealed class SetVariableRuleTests
{
    private readonly SetVariableRule _rule = new();

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
    public void Analyze_SelectVariableAssignment_NoDiagnostics()
    {
        // Arrange
        var sql = "DECLARE @Count INT; SELECT @Count = COUNT(*) FROM Users;";

        // Act
        var diagnostics = _rule.Analyze(CreateContext(sql)).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_SetVariableAssignment_ReturnsDiagnostic()
    {
        // Arrange
        var sql = "DECLARE @Count INT; SET @Count = 10;";

        // Act
        var diagnostics = _rule.Analyze(CreateContext(sql)).ToArray();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("semantic/set-variable", diagnostic.Code);
        Assert.Contains("SET", diagnostic.Message);
        Assert.Contains("SELECT", diagnostic.Message);
    }

    [Fact]
    public void Analyze_SetVariableWithExpression_ReturnsDiagnostic()
    {
        // Arrange
        var sql = "DECLARE @Total DECIMAL; SET @Total = 100 * 1.08;";

        // Act
        var diagnostics = _rule.Analyze(CreateContext(sql)).ToArray();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("semantic/set-variable", diagnostic.Code);
    }

    [Fact]
    public void Analyze_MultipleSetStatements_ReturnsMultipleDiagnostics()
    {
        // Arrange
        var sql = @"
DECLARE @A INT, @B INT;
SET @A = 1;
SET @B = 2;";

        // Act
        var diagnostics = _rule.Analyze(CreateContext(sql)).ToArray();

        // Assert
        Assert.Equal(2, diagnostics.Length);
        Assert.All(diagnostics, d => Assert.Equal("semantic/set-variable", d.Code));
    }

    [Fact]
    public void Analyze_SetNocountOn_NoDiagnostics()
    {
        // Arrange
        var sql = "SET NOCOUNT ON;";

        // Act
        var diagnostics = _rule.Analyze(CreateContext(sql)).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_SetAnsiNullsOn_NoDiagnostics()
    {
        // Arrange
        var sql = "SET ANSI_NULLS ON;";

        // Act
        var diagnostics = _rule.Analyze(CreateContext(sql)).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_SetQuotedIdentifierOn_NoDiagnostics()
    {
        // Arrange
        var sql = "SET QUOTED_IDENTIFIER ON;";

        // Act
        var diagnostics = _rule.Analyze(CreateContext(sql)).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }
}
