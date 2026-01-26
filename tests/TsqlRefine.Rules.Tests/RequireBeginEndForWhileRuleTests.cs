using System.IO;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules;

namespace TsqlRefine.Rules.Tests;

public sealed class RequireBeginEndForWhileRuleTests
{
    private readonly RequireBeginEndForWhileRule _rule = new();

    [Fact]
    public void Analyze_WhileWithoutBeginEnd_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = @"
            DECLARE @counter INT = 0;
            WHILE @counter < 10
                SET @counter = @counter + 1;
        ";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
        Assert.Equal("require-begin-end-for-while", diagnostics[0].Code);
        Assert.Contains("BEGIN/END", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_WhileWithBeginEnd_ReturnsNoDiagnostic()
    {
        // Arrange
        const string sql = @"
            DECLARE @counter INT = 0;
            WHILE @counter < 10
            BEGIN
                SET @counter = @counter + 1;
            END
        ";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_WhileWithMultipleStatements_WithBeginEnd_ReturnsNoDiagnostic()
    {
        // Arrange
        const string sql = @"
            DECLARE @counter INT = 0;
            WHILE @counter < 10
            BEGIN
                SET @counter = @counter + 1;
                PRINT @counter;
            END
        ";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_MultipleWhileWithoutBeginEnd_ReturnsMultipleDiagnostics()
    {
        // Arrange
        const string sql = @"
            DECLARE @counter INT = 0;
            WHILE @counter < 10
                SET @counter = @counter + 1;

            WHILE @counter > 0
                SET @counter = @counter - 1;
        ";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Equal(2, diagnostics.Length);
    }

    [Fact]
    public void Analyze_NestedWhileWithoutBeginEnd_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = @"
            DECLARE @outer INT = 0;
            DECLARE @inner INT = 0;
            WHILE @outer < 10
            BEGIN
                WHILE @inner < 5
                    SET @inner = @inner + 1;
                SET @outer = @outer + 1;
            END
        ";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
        Assert.Equal("require-begin-end-for-while", diagnostics[0].Code);
    }

    [Fact]
    public void Metadata_HasCorrectProperties()
    {
        // Assert
        Assert.Equal("require-begin-end-for-while", _rule.Metadata.RuleId);
        Assert.Equal("Control Flow Safety", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Warning, _rule.Metadata.DefaultSeverity);
        Assert.False(_rule.Metadata.Fixable);
    }

    private static RuleContext CreateContext(string sql)
    {
        var parser = new TSql150Parser(initialQuotedIdentifiers: true);

        using var fragmentReader = new StringReader(sql);
        var fragment = parser.Parse(fragmentReader, out IList<ParseError> parseErrors);

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
