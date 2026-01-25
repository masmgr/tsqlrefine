using System.IO;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules;

namespace TsqlRefine.Rules.Tests;

public sealed class UnicodeStringRuleTests
{
    private readonly UnicodeStringRule _rule = new();

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
    public void Analyze_NvarcharWithUnicodeString_NoDiagnostics()
    {
        // Arrange
        var sql = "DECLARE @Name NVARCHAR(50); SET @Name = 'こんにちは';";

        // Act
        var diagnostics = _rule.Analyze(CreateContext(sql)).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_VarcharWithAsciiString_NoDiagnostics()
    {
        // Arrange
        var sql = "DECLARE @Name VARCHAR(50); SET @Name = 'Hello';";

        // Act
        var diagnostics = _rule.Analyze(CreateContext(sql)).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_VarcharWithUnicodeString_ReturnsDiagnostic()
    {
        // Arrange
        var sql = "DECLARE @Name VARCHAR(50); SET @Name = 'こんにちは';";

        // Act
        var diagnostics = _rule.Analyze(CreateContext(sql)).ToArray();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("semantic/unicode-string", diagnostic.Code);
        Assert.Contains("Unicode", diagnostic.Message);
    }

    [Fact]
    public void Analyze_VarcharWithUnicodeLiteral_ReturnsDiagnostic()
    {
        // Arrange - Even with N prefix, VARCHAR can't properly store Unicode
        // The rule warns because VARCHAR is not suitable for Unicode data
        var sql = "DECLARE @Name VARCHAR(50); SET @Name = N'こんにちは';";

        // Act
        var diagnostics = _rule.Analyze(CreateContext(sql)).ToArray();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("semantic/unicode-string", diagnostic.Code);
    }

    [Fact]
    public void Analyze_VarcharWithEmojiString_ReturnsDiagnostic()
    {
        // Arrange
        var sql = "DECLARE @Message VARCHAR(100); SET @Message = 'Hello 😊';";

        // Act
        var diagnostics = _rule.Analyze(CreateContext(sql)).ToArray();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("semantic/unicode-string", diagnostic.Code);
    }

    [Fact]
    public void Analyze_IntVariable_NoDiagnostics()
    {
        // Arrange
        var sql = "DECLARE @Id INT; SET @Id = 123;";

        // Act
        var diagnostics = _rule.Analyze(CreateContext(sql)).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_UntypedLiteralWithUnicode_NoDiagnostics()
    {
        // Arrange - No variable declaration, just a literal in SELECT
        var sql = "SELECT 'こんにちは';";

        // Act
        var diagnostics = _rule.Analyze(CreateContext(sql)).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }
}
