using System.IO;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules;
using TsqlRefine.Rules.Rules.Performance;

namespace TsqlRefine.Rules.Tests.Performance;

public sealed class AvoidImplicitConversionInPredicateRuleTests
{
    private readonly AvoidImplicitConversionInPredicateRule _rule = new();

    [Fact]
    public void Analyze_ConvertOnColumnInWhere_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = "SELECT * FROM users WHERE CONVERT(VARCHAR(10), id) = '123';";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
        Assert.Equal("avoid-implicit-conversion-in-predicate", diagnostics[0].Code);
        Assert.Contains("CAST/CONVERT", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_CastOnColumnInWhere_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = "SELECT * FROM users WHERE CAST(id AS VARCHAR) = '123';";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
        Assert.Equal("avoid-implicit-conversion-in-predicate", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_UpperOnColumnInWhere_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = "SELECT * FROM users WHERE UPPER(name) = 'JOHN';";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
        Assert.Equal("avoid-implicit-conversion-in-predicate", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_SubstringOnColumnInWhere_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = "SELECT * FROM users WHERE SUBSTRING(name, 1, 3) = 'Joh';";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
        Assert.Equal("avoid-implicit-conversion-in-predicate", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_MultipleFunctionsInWhere_ReturnsMultipleDiagnostics()
    {
        // Arrange
        const string sql = "SELECT * FROM users WHERE UPPER(name) = 'JOHN' AND YEAR(created_date) = 2023;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Equal(2, diagnostics.Length);
        Assert.All(diagnostics, d => Assert.Equal("avoid-implicit-conversion-in-predicate", d.Code));
    }

    [Fact]
    public void Analyze_FunctionOnColumnInJoinCondition_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = "SELECT * FROM users u JOIN orders o ON CAST(u.id AS VARCHAR) = o.user_id;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
        Assert.Equal("avoid-implicit-conversion-in-predicate", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_FunctionOnLiteralInWhere_NoDiagnostic()
    {
        // Arrange
        const string sql = "SELECT * FROM users WHERE name = UPPER('john');";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_FunctionInSelectList_NoDiagnostic()
    {
        // Arrange
        const string sql = "SELECT UPPER(name), YEAR(created_date) FROM users;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_SimpleColumnComparison_NoDiagnostic()
    {
        // Arrange
        const string sql = "SELECT * FROM users WHERE id = 123;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_ColumnToColumnComparison_NoDiagnostic()
    {
        // Arrange
        const string sql = "SELECT * FROM users WHERE id = parent_id;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_FunctionOnBothSides_NoDiagnostic()
    {
        // Arrange
        const string sql = "SELECT * FROM users WHERE UPPER(name) = UPPER('john');";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        // Only the left side (column) should trigger
        Assert.Single(diagnostics);
    }

    [Fact]
    public void Analyze_EmptyInput_ReturnsEmpty()
    {
        // Arrange
        var context = CreateContext("");

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void GetFixes_ReturnsEmptyCollection()
    {
        // Arrange
        const string sql = "SELECT * FROM users WHERE UPPER(name) = 'JOHN';";
        var context = CreateContext(sql);
        var diagnostic = _rule.Analyze(context).First();

        // Act
        var fixes = _rule.GetFixes(context, diagnostic);

        // Assert
        Assert.Empty(fixes);
    }

    [Fact]
    public void Metadata_HasCorrectProperties()
    {
        // Assert
        Assert.Equal("avoid-implicit-conversion-in-predicate", _rule.Metadata.RuleId);
        Assert.Equal("Performance", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Warning, _rule.Metadata.DefaultSeverity);
        Assert.False(_rule.Metadata.Fixable);
    }

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
}
