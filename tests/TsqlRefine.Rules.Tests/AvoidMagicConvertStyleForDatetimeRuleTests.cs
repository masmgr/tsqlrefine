using System.IO;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules;

namespace TsqlRefine.Rules.Tests;

public sealed class AvoidMagicConvertStyleForDatetimeRuleTests
{
    private readonly AvoidMagicConvertStyleForDatetimeRule _rule = new();

    [Fact]
    public void Analyze_ConvertWithStyleNumber_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = "SELECT CONVERT(VARCHAR, GETDATE(), 101);";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
        Assert.Equal("avoid-magic-convert-style-for-datetime", diagnostics[0].Code);
        Assert.Contains("101", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_ConvertToDatetimeWithStyle_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = "SELECT CONVERT(DATETIME, '2023-01-01', 120);";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
        Assert.Equal("avoid-magic-convert-style-for-datetime", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_ConvertWithoutStyle_ReturnsNoDiagnostic()
    {
        // Arrange
        const string sql = "SELECT CONVERT(VARCHAR, GETDATE());";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_ConvertNonDatetimeType_ReturnsNoDiagnostic()
    {
        // Arrange
        const string sql = "SELECT CONVERT(INT, '123', 1);";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_FormatFunction_ReturnsNoDiagnostic()
    {
        // Arrange
        const string sql = "SELECT FORMAT(GETDATE(), 'yyyy-MM-dd');";
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
        Assert.Equal("avoid-magic-convert-style-for-datetime", _rule.Metadata.RuleId);
        Assert.Equal("Maintainability", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Information, _rule.Metadata.DefaultSeverity);
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
