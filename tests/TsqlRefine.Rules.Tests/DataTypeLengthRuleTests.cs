using System.IO;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules;

namespace TsqlRefine.Rules.Tests;

public sealed class DataTypeLengthRuleTests
{
    private readonly DataTypeLengthRule _rule = new();

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
    public void Analyze_VarcharWithLength_NoDiagnostics()
    {
        // Arrange
        var sql = "DECLARE @Name VARCHAR(50);";

        // Act
        var diagnostics = _rule.Analyze(CreateContext(sql)).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_VarcharWithoutLength_ReturnsDiagnostic()
    {
        // Arrange
        var sql = "DECLARE @Name VARCHAR;";

        // Act
        var diagnostics = _rule.Analyze(CreateContext(sql)).ToArray();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("semantic/data-type-length", diagnostic.Code);
        Assert.Contains("VARCHAR", diagnostic.Message);
    }

    [Fact]
    public void Analyze_NvarcharWithoutLength_ReturnsDiagnostic()
    {
        // Arrange
        var sql = "DECLARE @Name NVARCHAR;";

        // Act
        var diagnostics = _rule.Analyze(CreateContext(sql)).ToArray();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("semantic/data-type-length", diagnostic.Code);
        Assert.Contains("NVARCHAR", diagnostic.Message);
    }

    [Fact]
    public void Analyze_CharWithoutLength_ReturnsDiagnostic()
    {
        // Arrange
        var sql = "DECLARE @Code CHAR;";

        // Act
        var diagnostics = _rule.Analyze(CreateContext(sql)).ToArray();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("semantic/data-type-length", diagnostic.Code);
        Assert.Contains("CHAR", diagnostic.Message);
    }

    [Fact]
    public void Analyze_NcharWithoutLength_ReturnsDiagnostic()
    {
        // Arrange
        var sql = "DECLARE @Code NCHAR;";

        // Act
        var diagnostics = _rule.Analyze(CreateContext(sql)).ToArray();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("semantic/data-type-length", diagnostic.Code);
        Assert.Contains("NCHAR", diagnostic.Message);
    }

    [Fact]
    public void Analyze_VarbinaryWithoutLength_ReturnsDiagnostic()
    {
        // Arrange
        var sql = "DECLARE @Data VARBINARY;";

        // Act
        var diagnostics = _rule.Analyze(CreateContext(sql)).ToArray();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("semantic/data-type-length", diagnostic.Code);
        Assert.Contains("VARBINARY", diagnostic.Message);
    }

    [Fact]
    public void Analyze_BinaryWithoutLength_ReturnsDiagnostic()
    {
        // Arrange
        var sql = "DECLARE @Data BINARY;";

        // Act
        var diagnostics = _rule.Analyze(CreateContext(sql)).ToArray();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("semantic/data-type-length", diagnostic.Code);
        Assert.Contains("BINARY", diagnostic.Message);
    }

    [Fact]
    public void Analyze_VarcharMaxWithLength_NoDiagnostics()
    {
        // Arrange
        var sql = "DECLARE @Name VARCHAR(MAX);";

        // Act
        var diagnostics = _rule.Analyze(CreateContext(sql)).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_IntWithoutLength_NoDiagnostics()
    {
        // Arrange
        var sql = "DECLARE @Id INT;";

        // Act
        var diagnostics = _rule.Analyze(CreateContext(sql)).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_DateTimeWithoutLength_NoDiagnostics()
    {
        // Arrange
        var sql = "DECLARE @Created DATETIME;";

        // Act
        var diagnostics = _rule.Analyze(CreateContext(sql)).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_ColumnDefinitionWithoutLength_ReturnsDiagnostic()
    {
        // Arrange
        var sql = @"
CREATE TABLE Users (
    Name VARCHAR
);";

        // Act
        var diagnostics = _rule.Analyze(CreateContext(sql)).ToArray();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("semantic/data-type-length", diagnostic.Code);
    }

    [Fact]
    public void Analyze_MultipleVariablesWithoutLength_ReturnsMultipleDiagnostics()
    {
        // Arrange
        var sql = @"
DECLARE @Name VARCHAR;
DECLARE @Code CHAR;
DECLARE @Data VARBINARY;";

        // Act
        var diagnostics = _rule.Analyze(CreateContext(sql)).ToArray();

        // Assert
        Assert.Equal(3, diagnostics.Length);
        Assert.All(diagnostics, d => Assert.Equal("semantic/data-type-length", d.Code));
    }
}
