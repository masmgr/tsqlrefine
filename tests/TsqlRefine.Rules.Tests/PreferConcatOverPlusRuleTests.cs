using System.IO;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules;

namespace TsqlRefine.Rules.Tests;

public sealed class PreferConcatOverPlusRuleTests
{
    private readonly PreferConcatOverPlusRule _rule = new();

    [Fact]
    public void Analyze_PlusConcatenationWithIsnull_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = "SELECT ISNULL(@firstName, '') + ' ' + @lastName FROM users;";
        var context = CreateContext(sql, compatLevel: 110);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
        Assert.Equal("prefer-concat-over-plus", diagnostics[0].Code);
        Assert.Contains("CONCAT()", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_PlusConcatenationWithCoalesce_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = "SELECT COALESCE(@firstName, '') + ' ' + @lastName FROM users;";
        var context = CreateContext(sql, compatLevel: 110);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
        Assert.Equal("prefer-concat-over-plus", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_SimplePlusConcatenation_ReturnsNoDiagnostic()
    {
        // Arrange
        const string sql = "SELECT @firstName + ' ' + @lastName FROM users;";
        var context = CreateContext(sql, compatLevel: 110);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_ConcatFunction_ReturnsNoDiagnostic()
    {
        // Arrange
        const string sql = "SELECT CONCAT(@firstName, ' ', @lastName) FROM users;";
        var context = CreateContext(sql, compatLevel: 110);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_OldCompatLevel_ReturnsNoDiagnostic()
    {
        // Arrange
        const string sql = "SELECT ISNULL(@firstName, '') + ' ' + @lastName FROM users;";
        var context = CreateContext(sql, compatLevel: 100);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Metadata_HasCorrectProperties()
    {
        // Assert
        Assert.Equal("prefer-concat-over-plus", _rule.Metadata.RuleId);
        Assert.Equal("Modernization", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Information, _rule.Metadata.DefaultSeverity);
        Assert.False(_rule.Metadata.Fixable);
    }

    private static RuleContext CreateContext(string sql, int compatLevel = 150)
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
            CompatLevel: compatLevel,
            Ast: ast,
            Tokens: tokens,
            Settings: new RuleSettings()
        );
    }
}
