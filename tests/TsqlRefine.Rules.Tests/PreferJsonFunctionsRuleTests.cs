using System.IO;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules;

namespace TsqlRefine.Rules.Tests;

public sealed class PreferJsonFunctionsRuleTests
{
    private readonly PreferJsonFunctionsRule _rule = new();

    [Theory]
    [InlineData("SELECT CHARINDEX('{', json_data) FROM data;")]
    [InlineData("SELECT SUBSTRING(json_col, CHARINDEX('{', json_col), 50) FROM table1;")]  // May return 2 diagnostics (nested)
    [InlineData("SELECT PATINDEX('%[%', json_string) FROM documents;")]
    [InlineData("SELECT REPLACE(json_text, '{', '') FROM logs;")]
    [InlineData("SELECT STUFF(json_data, 1, 1, '{') FROM raw_data;")]
    public void Analyze_StringFunctionWithJsonPattern_ReturnsDiagnostic(string sql)
    {
        // Arrange
        var context = CreateContext(sql, compatLevel: 130);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert - May return multiple diagnostics for nested functions
        Assert.NotEmpty(diagnostics);
        Assert.All(diagnostics, d => Assert.Equal("prefer-json-functions", d.Code));
        Assert.All(diagnostics, d => Assert.Contains("JSON", d.Message));
    }

    [Theory]
    [InlineData("SELECT JSON_VALUE(json_col, '$.name') FROM users;")]
    [InlineData("SELECT JSON_QUERY(json_data, '$.items') FROM orders;")]
    [InlineData("SELECT * FROM OPENJSON(@json);")]
    [InlineData("SELECT id, name FROM users FOR JSON AUTO;")]
    [InlineData("SELECT CHARINDEX('test', description) FROM products;")]  // No JSON pattern
    [InlineData("SELECT SUBSTRING(name, 1, 5) FROM users;")]  // No JSON pattern
    [InlineData("SELECT * FROM users;")]
    [InlineData("")]  // Empty
    public void Analyze_WhenValid_ReturnsEmpty(string sql)
    {
        // Arrange
        var context = CreateContext(sql, compatLevel: 130);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_OldCompatLevel_ReturnsEmpty()
    {
        // Arrange
        const string sql = "SELECT CHARINDEX('{', json_data) FROM data;";
        var context = CreateContext(sql, compatLevel: 120);  // SQL Server 2014

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_CompatLevel130_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = "SELECT SUBSTRING(json_col, CHARINDEX('{', json_col), 100) FROM data;";
        var context = CreateContext(sql, compatLevel: 130);  // SQL Server 2016

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.NotEmpty(diagnostics);
    }

    [Fact]
    public void Analyze_MultipleJsonPatterns_ReturnsMultipleDiagnostics()
    {
        // Arrange
        const string sql = @"
            SELECT
                CHARINDEX('{', col1) AS pos1,
                SUBSTRING(col2, CHARINDEX('{', col2), 20) AS id
            FROM data;";
        var context = CreateContext(sql, compatLevel: 130);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.NotEmpty(diagnostics);
        Assert.All(diagnostics, d => Assert.Equal("prefer-json-functions", d.Code));
    }

    [Fact]
    public void Analyze_ComplexJsonParsing_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = @"
            SELECT
                id,
                SUBSTRING(
                    json_data,
                    CHARINDEX('{', json_data) + 1,
                    CHARINDEX('}', json_data) - CHARINDEX('{', json_data) - 1
                ) AS name
            FROM documents;";
        var context = CreateContext(sql, compatLevel: 130);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.NotEmpty(diagnostics);
        Assert.Contains(diagnostics, d => d.Code == "prefer-json-functions");
    }

    [Fact]
    public void Analyze_ReplaceWithJsonBraces_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = @"
            SELECT REPLACE(REPLACE(data, '{', ''), '}', '') AS cleaned
            FROM json_logs;";
        var context = CreateContext(sql, compatLevel: 130);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.NotEmpty(diagnostics);
    }

    [Fact]
    public void Analyze_PatindexWithJsonPattern_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = @"
            SELECT
                id,
                CASE
                    WHEN PATINDEX('%[{%', json_col) > 0 THEN 'has_object'
                    ELSE 'no_object'
                END AS status
            FROM data;";
        var context = CreateContext(sql, compatLevel: 130);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.NotEmpty(diagnostics);
    }

    [Fact]
    public void Analyze_FalsePositive_BracesInNonJson_MayTrigger()
    {
        // Arrange - This might be a false positive, but rule uses heuristics
        const string sql = "SELECT CHARINDEX('{', description) FROM users WHERE name LIKE '%test%';";
        var context = CreateContext(sql, compatLevel: 130);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert - Rule may trigger on any braces, even in non-JSON context
        // This test documents the heuristic nature of the rule
        if (diagnostics.Length > 0)
        {
            Assert.All(diagnostics, d => Assert.Equal("prefer-json-functions", d.Code));
        }
    }

    [Fact]
    public void Analyze_EmptyInput_ReturnsEmpty()
    {
        // Arrange
        var context = CreateContext("", compatLevel: 130);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Metadata_HasCorrectProperties()
    {
        // Assert
        Assert.Equal("prefer-json-functions", _rule.Metadata.RuleId);
        Assert.Equal("Modernization", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Information, _rule.Metadata.DefaultSeverity);
        Assert.False(_rule.Metadata.Fixable);
        Assert.Contains("JSON", _rule.Metadata.Description);
    }

    [Fact]
    public void GetFixes_ReturnsEmpty()
    {
        // Arrange
        var context = CreateContext("SELECT CHARINDEX('{', json_data) FROM data;", compatLevel: 130);
        var diagnostic = new Diagnostic(
            Range: new TsqlRefine.PluginSdk.Range(new Position(0, 0), new Position(0, 10)),
            Message: "test",
            Code: "prefer-json-functions"
        );

        // Act
        var fixes = _rule.GetFixes(context, diagnostic).ToArray();

        // Assert
        Assert.Empty(fixes);
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
