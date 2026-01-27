using System.IO;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules;
using TsqlRefine.Rules.Rules.Performance;

namespace TsqlRefine.Rules.Tests.Performance;

public sealed class ForbidTop100PercentOrderByRuleTests
{
    private readonly ForbidTop100PercentOrderByRule _rule = new();

    [Fact]
    public void Analyze_Top100PercentWithOrderBy_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = "SELECT TOP 100 PERCENT * FROM users ORDER BY id;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
        Assert.Equal("forbid-top-100-percent-order-by", diagnostics[0].Code);
        Assert.Contains("TOP 100 PERCENT", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_Top100PercentWithoutOrderBy_ReturnsNoDiagnostic()
    {
        // Arrange
        const string sql = "SELECT TOP 100 PERCENT * FROM users;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_Top50PercentWithOrderBy_ReturnsNoDiagnostic()
    {
        // Arrange
        const string sql = "SELECT TOP 50 PERCENT * FROM users ORDER BY id;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_TopWithoutPercent_ReturnsNoDiagnostic()
    {
        // Arrange
        const string sql = "SELECT TOP 100 * FROM users ORDER BY id;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_OrderByWithoutTop_ReturnsNoDiagnostic()
    {
        // Arrange
        const string sql = "SELECT * FROM users ORDER BY id;";
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
        Assert.Equal("forbid-top-100-percent-order-by", _rule.Metadata.RuleId);
        Assert.Equal("Performance", _rule.Metadata.Category);
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
