using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Correctness;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Correctness;

public sealed class AvoidAmbiguousDatetimeLiteralRuleTests
{
    private readonly AvoidAmbiguousDatetimeLiteralRule _rule = new();

    [Theory]
    [InlineData("SELECT * FROM orders WHERE order_date = '12/31/2023';")]
    [InlineData("SELECT * FROM users WHERE created_at > '1/1/23';")]
    [InlineData("SELECT * FROM events WHERE event_date = '31/12/2023';")]
    [InlineData("SELECT * FROM logs WHERE log_date = '3/15/2024';")]
    [InlineData("SELECT '12/31/2023' AS date_value;")]
    [InlineData("SELECT \"12/31/2023\" AS date_value;")]  // Double quotes
    public void Analyze_WhenSlashDelimitedDateLiteral_ReturnsDiagnostic(string sql)
    {
        // Arrange
        var context = RuleTestContext.CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
        Assert.Equal("avoid-ambiguous-datetime-literal", diagnostics[0].Code);
        Assert.Contains("slash-delimited", diagnostics[0].Message);
        Assert.Contains("ISO 8601", diagnostics[0].Message);
    }

    [Theory]
    [InlineData("SELECT * FROM orders WHERE order_date = '2023-12-31';")]  // ISO 8601
    [InlineData("SELECT * FROM users WHERE created_at > '2023-01-01';")]
    [InlineData("SELECT * FROM events WHERE event_date = '2024-03-15';")]
    [InlineData("SELECT * FROM logs WHERE log_date = '20231231';")]  // YYYYMMDD
    [InlineData("SELECT * FROM users;")]
    [InlineData("SELECT 'This is 12/31 not a date' AS text_value;")]  // Not a full date
    [InlineData("SELECT 'Price: $12/unit' AS description;")]  // Not a date
    [InlineData("SELECT name, email FROM users;")]
    [InlineData("")]  // Empty
    public void Analyze_WhenValid_ReturnsEmpty(string sql)
    {
        // Arrange
        var context = RuleTestContext.CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_MultipleDateLiterals_ReturnsMultipleDiagnostics()
    {
        // Arrange
        const string sql = @"
            SELECT * FROM orders
            WHERE order_date BETWEEN '1/1/2023' AND '12/31/2023';";
        var context = RuleTestContext.CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Equal(2, diagnostics.Length);
        Assert.All(diagnostics, d => Assert.Equal("avoid-ambiguous-datetime-literal", d.Code));
    }

    [Fact]
    public void Analyze_DateInComment_ReturnsEmpty()
    {
        // Arrange
        const string sql = @"
            -- Date format: 12/31/2023
            SELECT * FROM users WHERE created_at > '2023-12-31';";
        var context = RuleTestContext.CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_MixedFormats_ReturnsOnlySlashDelimited()
    {
        // Arrange
        const string sql = @"
            SELECT * FROM orders
            WHERE order_date = '12/31/2023'
               OR order_date = '2023-12-31';";
        var context = RuleTestContext.CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
        Assert.Contains("12/31/2023", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_EmptyInput_ReturnsEmpty()
    {
        // Arrange
        var context = RuleTestContext.CreateContext("");

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Metadata_HasCorrectProperties()
    {
        // Assert
        Assert.Equal("avoid-ambiguous-datetime-literal", _rule.Metadata.RuleId);
        Assert.Equal("Correctness", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Warning, _rule.Metadata.DefaultSeverity);
        Assert.False(_rule.Metadata.Fixable);
        Assert.Contains("ISO 8601", _rule.Metadata.Description);
    }

    [Fact]
    public void GetFixes_ReturnsEmpty()
    {
        // Arrange
        var context = RuleTestContext.CreateContext("SELECT * FROM orders WHERE order_date = '12/31/2023';");
        var diagnostic = new Diagnostic(
            Range: new TsqlRefine.PluginSdk.Range(new Position(0, 0), new Position(0, 10)),
            Message: "test",
            Code: "avoid-ambiguous-datetime-literal"
        );

        // Act
        var fixes = _rule.GetFixes(context, diagnostic).ToArray();

        // Assert
        Assert.Empty(fixes);
    }

}
