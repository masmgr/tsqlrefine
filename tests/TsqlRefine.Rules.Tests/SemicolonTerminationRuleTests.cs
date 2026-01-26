using System.IO;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules;

namespace TsqlRefine.Rules.Tests;

public sealed class SemicolonTerminationRuleTests
{
    private readonly SemicolonTerminationRule _rule = new();

    [Fact]
    public void Analyze_SelectWithoutSemicolon_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = "SELECT 1";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
        Assert.Equal("semicolon-termination", diagnostics[0].Code);
        Assert.Contains("semicolon", diagnostics[0].Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Analyze_SelectWithSemicolon_ReturnsNoDiagnostic()
    {
        // Arrange
        const string sql = "SELECT 1;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_MultipleStatementsWithoutSemicolon_ReturnsDiagnostics()
    {
        // Arrange
        const string sql = @"
SELECT 1
SELECT 2
SELECT 3;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Equal(2, diagnostics.Length); // First two statements lack semicolons
    }

    [Fact]
    public void Analyze_MultipleStatementsWithSemicolons_ReturnsNoDiagnostic()
    {
        // Arrange
        const string sql = @"
SELECT 1;
SELECT 2;
SELECT 3;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_UpdateWithoutSemicolon_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = "UPDATE users SET active = 1 WHERE id = 1";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
        Assert.Equal("semicolon-termination", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_InsertWithSemicolon_ReturnsNoDiagnostic()
    {
        // Arrange
        const string sql = "INSERT INTO users (name) VALUES ('test');";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_DeleteWithoutSemicolon_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = "DELETE FROM users WHERE id = 1";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
    }

    [Fact]
    public void Analyze_CreateProcedureBody_StatementsWithoutSemicolon_ReturnsDiagnostics()
    {
        // Arrange
        const string sql = @"
CREATE PROCEDURE GetUser
AS
BEGIN
    SELECT 1
    SELECT 2;
END";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics); // Only the first SELECT lacks semicolon
    }

    [Fact]
    public void Analyze_CTEWithoutSemicolon_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = @"
WITH cte AS (SELECT 1 AS x)
SELECT * FROM cte";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
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
        Assert.Equal("semicolon-termination", _rule.Metadata.RuleId);
        Assert.Equal("Style", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Information, _rule.Metadata.DefaultSeverity);
        Assert.True(_rule.Metadata.Fixable);
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
