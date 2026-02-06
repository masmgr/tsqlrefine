using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Correctness;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Correctness;

public sealed class StringAggWithoutOrderByRuleTests
{
    private readonly StringAggWithoutOrderByRule _rule = new();

    [Theory]
    [InlineData("SELECT STRING_AGG(name, ',') FROM users;")]
    [InlineData("SELECT STRING_AGG(name, ', ') AS names FROM users;")]
    [InlineData("SELECT id, STRING_AGG(tag, '; ') AS tags FROM items GROUP BY id;")]
    [InlineData("SELECT STRING_AGG(CAST(id AS VARCHAR(10)), ',') FROM users;")]
    public void Analyze_StringAggWithoutOrderBy_ReturnsDiagnostic(string sql)
    {
        // Arrange
        var context = RuleTestContext.CreateContext(sql, compatLevel: 140);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
        Assert.Equal("string-agg-without-order-by", diagnostics[0].Code);
        Assert.Contains("ORDER BY", diagnostics[0].Message);
    }

    [Theory]
    [InlineData("SELECT STRING_AGG(name, ',') WITHIN GROUP (ORDER BY name) FROM users;")]
    [InlineData("SELECT STRING_AGG(name, ', ') WITHIN GROUP (ORDER BY name DESC) AS names FROM users;")]
    [InlineData("SELECT id, STRING_AGG(tag, '; ') WITHIN GROUP (ORDER BY tag ASC) AS tags FROM items GROUP BY id;")]
    public void Analyze_StringAggWithOrderBy_ReturnsEmpty(string sql)
    {
        // Arrange
        var context = RuleTestContext.CreateContext(sql, compatLevel: 140);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Theory]
    [InlineData("SELECT COUNT(*) FROM users;")]  // Different aggregate
    [InlineData("SELECT SUM(amount) FROM orders;")]  // Different aggregate
    [InlineData("SELECT * FROM users;")]  // Plain query
    [InlineData("")]  // Empty
    public void Analyze_WhenNotStringAgg_ReturnsEmpty(string sql)
    {
        // Arrange
        var context = RuleTestContext.CreateContext(sql, compatLevel: 140);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_MultipleStringAggWithoutOrderBy_ReturnsMultipleDiagnostics()
    {
        // Arrange
        const string sql = @"
            SELECT
                STRING_AGG(name, ',') AS names,
                STRING_AGG(email, '; ') AS emails
            FROM users;";
        var context = RuleTestContext.CreateContext(sql, compatLevel: 140);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Equal(2, diagnostics.Length);
        Assert.All(diagnostics, d => Assert.Equal("string-agg-without-order-by", d.Code));
    }

    [Fact]
    public void Analyze_MixedWithAndWithoutOrderBy_ReturnsOneDiagnostic()
    {
        // Arrange
        const string sql = @"
            SELECT
                STRING_AGG(name, ',') WITHIN GROUP (ORDER BY name) AS names,
                STRING_AGG(email, '; ') AS emails
            FROM users;";
        var context = RuleTestContext.CreateContext(sql, compatLevel: 140);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
        Assert.Equal("string-agg-without-order-by", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_StringAggInSubquery_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = @"
            SELECT *
            FROM (
                SELECT id, STRING_AGG(tag, ',') AS tags
                FROM items
                GROUP BY id
            ) AS sub;";
        var context = RuleTestContext.CreateContext(sql, compatLevel: 140);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
        Assert.Equal("string-agg-without-order-by", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_StringAggInCte_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = @"
            WITH cte AS (
                SELECT id, STRING_AGG(tag, ',') AS tags
                FROM items
                GROUP BY id
            )
            SELECT * FROM cte;";
        var context = RuleTestContext.CreateContext(sql, compatLevel: 140);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
        Assert.Equal("string-agg-without-order-by", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_BelowCompatLevel140_ReturnsEmpty()
    {
        // Arrange - STRING_AGG is SQL Server 2017+ (compat level 140+)
        const string sql = "SELECT STRING_AGG(name, ',') FROM users;";
        var context = RuleTestContext.CreateContext(sql, compatLevel: 130);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_EmptyInput_ReturnsEmpty()
    {
        // Arrange
        var context = RuleTestContext.CreateContext("", compatLevel: 140);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Metadata_HasCorrectProperties()
    {
        // Assert
        Assert.Equal("string-agg-without-order-by", _rule.Metadata.RuleId);
        Assert.Equal("Correctness", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Warning, _rule.Metadata.DefaultSeverity);
        Assert.False(_rule.Metadata.Fixable);
        Assert.Contains("STRING_AGG", _rule.Metadata.Description);
        Assert.Contains("ORDER BY", _rule.Metadata.Description);
    }

    [Fact]
    public void GetFixes_ReturnsEmpty()
    {
        // Arrange
        var context = RuleTestContext.CreateContext("SELECT STRING_AGG(name, ',') FROM users;", compatLevel: 140);
        var diagnostic = new Diagnostic(
            Range: new TsqlRefine.PluginSdk.Range(new Position(0, 0), new Position(0, 10)),
            Message: "test",
            Code: "string-agg-without-order-by"
        );

        // Act
        var fixes = _rule.GetFixes(context, diagnostic).ToArray();

        // Assert
        Assert.Empty(fixes);
    }
}
