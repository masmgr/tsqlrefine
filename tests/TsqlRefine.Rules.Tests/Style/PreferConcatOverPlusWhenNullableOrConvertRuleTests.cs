using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Style;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Style;

public sealed class PreferConcatOverPlusWhenNullableOrConvertRuleTests
{
    private readonly PreferConcatOverPlusWhenNullableOrConvertRule _rule = new();

    [Theory]
    [InlineData("SELECT ISNULL(first_name, '') + ' ' + ISNULL(last_name, '') AS full_name FROM users;")]
    [InlineData("SELECT CAST(id AS VARCHAR) + ': ' + name FROM items;")]
    [InlineData("SELECT CONVERT(VARCHAR, order_id) + ' - ' + description FROM orders;")]
    [InlineData("select isnull(name, '') + ' test' from users;")]  // lowercase
    public void Analyze_PlusConcatenationWithNullHandlingOrConversion_ReturnsDiagnostic(string sql)
    {
        // Arrange
        var context = CreateContext(sql, compatLevel: 110);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.NotEmpty(diagnostics);
        Assert.All(diagnostics, d => Assert.Equal("prefer-concat-with-nullable", d.Code));
        Assert.All(diagnostics, d => Assert.Contains("CONCAT", d.Message));
    }

    [Theory]
    [InlineData("SELECT CONCAT(first_name, ' ', last_name) AS full_name FROM users;")]
    [InlineData("SELECT first_name + ' ' + last_name FROM users;")]  // Simple concatenation, no NULL handling
    [InlineData("SELECT name + description FROM products;")]
    [InlineData("SELECT * FROM users;")]
    [InlineData("")]  // Empty
    public void Analyze_WhenValid_ReturnsEmpty(string sql)
    {
        // Arrange
        var context = CreateContext(sql, compatLevel: 110);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_OldCompatLevel_ReturnsEmpty()
    {
        // Arrange
        const string sql = "SELECT ISNULL(first_name, '') + ' ' + ISNULL(last_name, '') FROM users;";
        var context = CreateContext(sql, compatLevel: 100);  // SQL Server 2008

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_CompatLevel110_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = "SELECT CAST(id AS VARCHAR) + name FROM users;";
        var context = CreateContext(sql, compatLevel: 110);  // SQL Server 2012

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
    }

    [Fact]
    public void Analyze_MultipleConcatenations_ReturnsMultipleDiagnostics()
    {
        // Arrange
        const string sql = @"
            SELECT
                ISNULL(col1, '') + col2,
                CAST(col3 AS VARCHAR) + col4
            FROM data;";
        var context = CreateContext(sql, compatLevel: 110);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Equal(2, diagnostics.Length);
        Assert.All(diagnostics, d => Assert.Equal("prefer-concat-with-nullable", d.Code));
    }

    [Fact]
    public void Analyze_NestedIsnull_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = "SELECT ISNULL(ISNULL(name, alias), 'N/A') + ' test' FROM users;";
        var context = CreateContext(sql, compatLevel: 110);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
    }

    [Fact]
    public void Analyze_ConvertInComplexExpression_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = @"
            SELECT
                'Order: ' + CONVERT(VARCHAR, order_id) + ' - Total: ' + CONVERT(VARCHAR, total)
            FROM orders;";
        var context = CreateContext(sql, compatLevel: 110);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.NotEmpty(diagnostics);
    }

    [Fact]
    public void Analyze_EmptyInput_ReturnsEmpty()
    {
        // Arrange
        var context = CreateContext("", compatLevel: 110);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Metadata_HasCorrectProperties()
    {
        // Assert
        Assert.Equal("prefer-concat-with-nullable", _rule.Metadata.RuleId);
        Assert.Equal("Style", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Information, _rule.Metadata.DefaultSeverity);
        Assert.False(_rule.Metadata.Fixable);
        Assert.Contains("SQL Server 2012", _rule.Metadata.Description);
    }

    [Fact]
    public void GetFixes_ReturnsEmpty()
    {
        // Arrange
        var context = CreateContext("SELECT CAST(id AS VARCHAR) + name FROM users;", compatLevel: 110);
        var diagnostic = new Diagnostic(
            Range: new TsqlRefine.PluginSdk.Range(new Position(0, 0), new Position(0, 10)),
            Message: "test",
            Code: "prefer-concat-with-nullable"
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
