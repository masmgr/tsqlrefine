using System.IO;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules;
using TsqlRefine.Rules.Rules.Style;

namespace TsqlRefine.Rules.Tests.Style;

public sealed class PreferCoalesceOverNestedIsnullRuleTests
{
    private readonly PreferCoalesceOverNestedIsnullRule _rule = new();

    [Fact]
    public void Analyze_NestedIsnull_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = "SELECT ISNULL(ISNULL(@value1, @value2), @value3) FROM users;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
        Assert.Equal("prefer-coalesce-over-nested-isnull", diagnostics[0].Code);
        Assert.Contains("COALESCE", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_SingleIsnull_ReturnsNoDiagnostic()
    {
        // Arrange
        const string sql = "SELECT ISNULL(@value, 'default') FROM users;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_Coalesce_ReturnsNoDiagnostic()
    {
        // Arrange
        const string sql = "SELECT COALESCE(@value1, @value2, @value3) FROM users;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_DeepNesting_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = "SELECT ISNULL(col1, ISNULL(col2, ISNULL(col3, 'default'))) FROM users;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.True(diagnostics.Length >= 1);
        Assert.All(diagnostics, d => Assert.Equal("prefer-coalesce-over-nested-isnull", d.Code));
    }

    [Fact]
    public void Metadata_HasCorrectProperties()
    {
        // Assert
        Assert.Equal("prefer-coalesce-over-nested-isnull", _rule.Metadata.RuleId);
        Assert.Equal("Style", _rule.Metadata.Category);
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
