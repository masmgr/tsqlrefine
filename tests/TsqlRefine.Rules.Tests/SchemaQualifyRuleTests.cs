using System.IO;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules;

namespace TsqlRefine.Rules.Tests;

public sealed class SchemaQualifyRuleTests
{
    private readonly SchemaQualifyRule _rule = new();

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
    public void Analyze_TableWithSchema_NoDiagnostics()
    {
        // Arrange
        var sql = "SELECT * FROM dbo.Users;";

        // Act
        var diagnostics = _rule.Analyze(CreateContext(sql)).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_TableWithoutSchema_ReturnsDiagnostic()
    {
        // Arrange
        var sql = "SELECT * FROM Users;";

        // Act
        var diagnostics = _rule.Analyze(CreateContext(sql)).ToArray();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("semantic/schema-qualify", diagnostic.Code);
        Assert.Contains("schema", diagnostic.Message);
    }

    [Fact]
    public void Analyze_TempTable_NoDiagnostics()
    {
        // Arrange
        var sql = "SELECT * FROM #TempUsers;";

        // Act
        var diagnostics = _rule.Analyze(CreateContext(sql)).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_GlobalTempTable_NoDiagnostics()
    {
        // Arrange
        var sql = "SELECT * FROM ##GlobalTemp;";

        // Act
        var diagnostics = _rule.Analyze(CreateContext(sql)).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_TableVariable_NoDiagnostics()
    {
        // Arrange
        var sql = "DECLARE @Users TABLE (Id INT); SELECT * FROM @Users;";

        // Act
        var diagnostics = _rule.Analyze(CreateContext(sql)).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_SystemTable_NoDiagnostics()
    {
        // Arrange
        var sql = "SELECT * FROM sys.tables;";

        // Act
        var diagnostics = _rule.Analyze(CreateContext(sql)).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_MultipleTablesWithoutSchema_ReturnsMultipleDiagnostics()
    {
        // Arrange
        var sql = "SELECT * FROM Users JOIN Orders ON Users.Id = Orders.UserId;";

        // Act
        var diagnostics = _rule.Analyze(CreateContext(sql)).ToArray();

        // Assert
        Assert.Equal(2, diagnostics.Length);
        Assert.All(diagnostics, d => Assert.Equal("semantic/schema-qualify", d.Code));
    }

    [Fact]
    public void Analyze_JoinWithMixedSchemaQualification_ReturnsDiagnosticForUnqualified()
    {
        // Arrange
        var sql = "SELECT * FROM dbo.Users JOIN Orders ON Users.Id = Orders.UserId;";

        // Act
        var diagnostics = _rule.Analyze(CreateContext(sql)).ToArray();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("semantic/schema-qualify", diagnostic.Code);
        Assert.Contains("Orders", diagnostic.Message);
    }
}
