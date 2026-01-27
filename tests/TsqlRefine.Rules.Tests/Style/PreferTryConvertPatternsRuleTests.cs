using System.IO;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules;
using TsqlRefine.Rules.Rules.Style;

namespace TsqlRefine.Rules.Tests.Style;

public sealed class PreferTryConvertPatternsRuleTests
{
    private readonly PreferTryConvertPatternsRule _rule = new();

    [Theory]
    [InlineData("SELECT CASE WHEN ISNUMERIC(@value) = 1 THEN CONVERT(INT, @value) ELSE NULL END;")]
    [InlineData("SELECT CASE WHEN ISDATE(@value) = 1 THEN CAST(@value AS DATE) ELSE NULL END;")]
    [InlineData("SELECT CASE WHEN ISNUMERIC(col) = 1 THEN CONVERT(DECIMAL(10,2), col) END FROM data;")]
    [InlineData("select case when isnumeric(@val) = 1 then convert(int, @val) end;")]  // lowercase
    public void Analyze_WhenCaseWithValidationAndConversion_ReturnsDiagnostic(string sql)
    {
        // Arrange
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
        Assert.Equal("prefer-try-convert-patterns", diagnostics[0].Code);
        Assert.Contains("TRY_CONVERT", diagnostics[0].Message);
    }

    [Theory]
    [InlineData("SELECT TRY_CONVERT(INT, @value);")]
    [InlineData("SELECT TRY_CAST(@value AS DATE);")]
    [InlineData("SELECT CONVERT(INT, @value);")]
    [InlineData("SELECT CAST(@value AS DATE);")]
    [InlineData("SELECT CASE WHEN @value > 10 THEN 'high' ELSE 'low' END;")]
    [InlineData("SELECT CASE WHEN LEN(name) > 0 THEN UPPER(name) END FROM users;")]
    [InlineData("SELECT * FROM users;")]
    [InlineData("")]  // Empty
    public void Analyze_WhenValid_ReturnsEmpty(string sql)
    {
        // Arrange
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_IsnumericWithoutConversion_ReturnsEmpty()
    {
        // Arrange
        const string sql = "SELECT CASE WHEN ISNUMERIC(@value) = 1 THEN 'valid' ELSE 'invalid' END;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_ConversionWithoutValidation_ReturnsEmpty()
    {
        // Arrange
        const string sql = "SELECT CASE WHEN @flag = 1 THEN CONVERT(INT, @value) ELSE NULL END;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_MultipleCaseStatements_ReturnsMultipleDiagnostics()
    {
        // Arrange
        const string sql = @"
            SELECT
                CASE WHEN ISNUMERIC(col1) = 1 THEN CONVERT(INT, col1) END AS num1,
                CASE WHEN ISDATE(col2) = 1 THEN CAST(col2 AS DATE) END AS date1
            FROM data;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Equal(2, diagnostics.Length);
        Assert.All(diagnostics, d => Assert.Equal("prefer-try-convert-patterns", d.Code));
    }

    [Fact]
    public void Analyze_NestedCase_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = @"
            SELECT
                CASE
                    WHEN type = 'int' THEN
                        CASE WHEN ISNUMERIC(value) = 1 THEN CONVERT(INT, value) END
                    ELSE NULL
                END
            FROM data;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.NotEmpty(diagnostics);
        Assert.Equal("prefer-try-convert-patterns", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_ComplexWhenClause_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = @"
            SELECT CASE
                WHEN ISNUMERIC(@value) = 1 AND LEN(@value) > 0
                THEN CONVERT(INT, @value)
                ELSE 0
            END;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
        Assert.Equal("prefer-try-convert-patterns", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_IsdatePattern_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = @"
            SELECT CASE
                WHEN ISDATE(date_string) = 1
                THEN CONVERT(DATETIME, date_string)
            END
            FROM logs;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
        Assert.Equal("prefer-try-convert-patterns", diagnostics[0].Code);
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
    public void Metadata_HasCorrectProperties()
    {
        // Assert
        Assert.Equal("prefer-try-convert-patterns", _rule.Metadata.RuleId);
        Assert.Equal("Style", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Information, _rule.Metadata.DefaultSeverity);
        Assert.False(_rule.Metadata.Fixable);
    }

    [Fact]
    public void GetFixes_ReturnsEmpty()
    {
        // Arrange
        var context = CreateContext("SELECT CASE WHEN ISNUMERIC(@value) = 1 THEN CONVERT(INT, @value) END;");
        var diagnostic = new Diagnostic(
            Range: new TsqlRefine.PluginSdk.Range(new Position(0, 0), new Position(0, 10)),
            Message: "test",
            Code: "prefer-try-convert-patterns"
        );

        // Act
        var fixes = _rule.GetFixes(context, diagnostic).ToArray();

        // Assert
        Assert.Empty(fixes);
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
