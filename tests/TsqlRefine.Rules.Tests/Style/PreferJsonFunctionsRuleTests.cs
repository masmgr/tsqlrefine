using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Style;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Style;

public sealed class PreferJsonFunctionsRuleTests
{
    private readonly PreferJsonFunctionsRule _rule = new();

    [Theory]
    [InlineData("""SELECT CHARINDEX('{"name":', json_data) FROM data;""")]
    [InlineData("""SELECT REPLACE(json_text, '{"', '') FROM logs;""")]
    [InlineData("""SELECT CHARINDEX('":', json_data) FROM data;""")]
    [InlineData("SELECT STUFF(json_data, 1, 1, '[{') FROM raw_data;")]
    [InlineData("""SELECT PATINDEX('%{"name%', json_string) FROM documents;""")]
    public void Analyze_StringFunctionWithJsonPattern_ReturnsDiagnostic(string sql)
    {
        // Arrange
        var context = CreateContext(sql, compatLevel: 130);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.NotEmpty(diagnostics);
        Assert.All(diagnostics, d => Assert.Equal("prefer-json-functions", d.Code));
        Assert.All(diagnostics, d => Assert.Contains("JSON", d.Message));
    }

    [Theory]
    [InlineData("SELECT JSON_VALUE(json_col, '$.name') FROM users;")]
    [InlineData("SELECT JSON_QUERY(json_data, '$.items') FROM orders;")]
    [InlineData("SELECT * FROM OPENJSON(@json);")]
    [InlineData("SELECT id, name FROM users FOR JSON AUTO;")]
    [InlineData("SELECT CHARINDEX('test', description) FROM products;")]
    [InlineData("SELECT SUBSTRING(name, 1, 5) FROM users;")]
    [InlineData("SELECT * FROM users;")]
    [InlineData("")]
    [InlineData("SELECT CHARINDEX('{', json_data) FROM data;")]                     // Single brace, no pair
    [InlineData("SELECT REPLACE(json_text, '{', '') FROM logs;")]                    // Single brace, no pair
    [InlineData("SELECT STUFF(json_data, 1, 1, '{') FROM raw_data;")]               // Single brace, no pair
    [InlineData("SELECT PATINDEX('%[^0]%', col1 + '0') FROM t;")]                   // PATINDEX wildcard
    [InlineData("SELECT PATINDEX('%[a-z]%', col1) FROM t;")]                        // Character class
    [InlineData("SELECT PATINDEX('%[0-9]%', col1) FROM t;")]                        // Digit class
    [InlineData("SELECT CHARINDEX('[', col1) FROM t;")]                             // Single bracket, no pair
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
        const string sql = """SELECT CHARINDEX('{"key":', json_data) FROM data;""";
        var context = CreateContext(sql, compatLevel: 120);  // SQL Server 2014

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_CompatLevel130_ReturnsDiagnostic()
    {
        // Arrange — paired braces across nested function parameters
        const string sql = "SELECT SUBSTRING(json_col, CHARINDEX('{', json_col), CHARINDEX('}', json_col)) FROM data;";
        var context = CreateContext(sql, compatLevel: 130);  // SQL Server 2016

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.NotEmpty(diagnostics);
    }

    [Fact]
    public void Analyze_MultipleJsonPatterns_ReturnsMultipleDiagnostics()
    {
        // Arrange — strong JSON patterns in multiple function calls
        const string sql = """
            SELECT
                CHARINDEX('":', col1) AS pos1,
                REPLACE(col2, '{"id":', '') AS id
            FROM data;
            """;
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
        // Arrange — paired braces '{' and '}' across nested CHARINDEX parameters
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
        // Arrange — nested REPLACE with both '{' and '}' across parameters
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
        // Arrange — '%[{%' contains '[{' which is a strong JSON array-of-objects pattern
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
    public void Analyze_SingleBraceInNonJsonContext_ReturnsEmpty()
    {
        // Arrange — single '{' without a matching '}' is not sufficient evidence
        const string sql = "SELECT CHARINDEX('{', description) FROM users WHERE name LIKE '%test%';";
        var context = CreateContext(sql, compatLevel: 130);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_PatindexLeadingZeroStrip_ReturnsEmpty()
    {
        // Arrange — leading zero stripping pattern (the reported false positive)
        const string sql = "SELECT STUFF(COLUMN1, 1, PATINDEX('%[^0]%', COLUMN1 + '0') - 1, '') FROM t;";
        var context = CreateContext(sql, compatLevel: 130);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
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
        Assert.Equal("Style", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Information, _rule.Metadata.DefaultSeverity);
        Assert.False(_rule.Metadata.Fixable);
        Assert.Contains("JSON", _rule.Metadata.Description);
    }

    [Fact]
    public void GetFixes_ReturnsEmpty()
    {
        // Arrange
        var context = CreateContext("""SELECT CHARINDEX('{"key":', json_data) FROM data;""", compatLevel: 130);
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
        return RuleTestContext.CreateContext(sql, compatLevel);
    }
}
