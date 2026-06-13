using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Correctness;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Correctness;

public sealed class StringAggNvarcharMaxRuleTests
{
    private readonly StringAggNvarcharMaxRule _rule = new();

    [Theory]
    [InlineData("SELECT STRING_AGG(name, ',') FROM users;")]  // Bare column reference
    [InlineData("SELECT STRING_AGG(CAST(id AS VARCHAR(10)), ',') FROM users;")]  // CAST to varchar
    [InlineData("SELECT STRING_AGG(CAST(name AS NVARCHAR(100)), ',') FROM users;")]  // Sized nvarchar(n)
    [InlineData("SELECT STRING_AGG(CONVERT(VARCHAR(MAX), name), ',') FROM users;")]  // varchar(max)
    [InlineData("SELECT STRING_AGG('literal', ',') FROM users;")]  // String literal
    public void Analyze_FirstArgumentNotNvarcharMax_ReturnsDiagnostic(string sql)
    {
        // Arrange
        var context = RuleTestContext.CreateContext(sql, compatLevel: 140);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
        Assert.Equal("string-agg-nvarchar-max", diagnostics[0].Code);
        Assert.Contains("NVARCHAR(MAX)", diagnostics[0].Message);
    }

    [Theory]
    [InlineData("SELECT STRING_AGG(CAST(name AS NVARCHAR(MAX)), ',') FROM users;")]  // CAST to nvarchar(max)
    [InlineData("SELECT STRING_AGG(CONVERT(NVARCHAR(MAX), name), ',') FROM users;")]  // CONVERT to nvarchar(max)
    [InlineData("SELECT STRING_AGG(TRY_CAST(name AS NVARCHAR(MAX)), ',') FROM users;")]  // TRY_CAST
    [InlineData("SELECT STRING_AGG(TRY_CONVERT(NVARCHAR(MAX), name), ',') FROM users;")]  // TRY_CONVERT
    public void Analyze_FirstArgumentIsNvarcharMax_ReturnsEmpty(string sql)
    {
        // Arrange
        var context = RuleTestContext.CreateContext(sql, compatLevel: 140);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_WithinGroupOrderByDoesNotAffectResult_ReturnsEmpty()
    {
        // Arrange - WITHIN GROUP (ORDER BY) is orthogonal; cast to nvarchar(max) is still fine
        const string sql = "SELECT STRING_AGG(CAST(name AS NVARCHAR(MAX)), ',') WITHIN GROUP (ORDER BY name) FROM users;";
        var context = RuleTestContext.CreateContext(sql, compatLevel: 140);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_WithinGroupOrderByWithoutCast_ReturnsDiagnostic()
    {
        // Arrange - ORDER BY present but first arg is still not nvarchar(max)
        const string sql = "SELECT STRING_AGG(name, ',') WITHIN GROUP (ORDER BY name) FROM users;";
        var context = RuleTestContext.CreateContext(sql, compatLevel: 140);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
        Assert.Equal("string-agg-nvarchar-max", diagnostics[0].Code);
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
    public void Analyze_MultipleStringAggWithoutNvarcharMax_ReturnsMultipleDiagnostics()
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
        Assert.All(diagnostics, d => Assert.Equal("string-agg-nvarchar-max", d.Code));
    }

    [Fact]
    public void Analyze_MixedCastAndBare_ReturnsOneDiagnostic()
    {
        // Arrange
        const string sql = @"
            SELECT
                STRING_AGG(CAST(name AS NVARCHAR(MAX)), ',') AS names,
                STRING_AGG(email, '; ') AS emails
            FROM users;";
        var context = RuleTestContext.CreateContext(sql, compatLevel: 140);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
        Assert.Equal("string-agg-nvarchar-max", diagnostics[0].Code);
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
        Assert.Equal("string-agg-nvarchar-max", diagnostics[0].Code);
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
        Assert.Equal("string-agg-nvarchar-max", _rule.Metadata.RuleId);
        Assert.Equal("Correctness", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Warning, _rule.Metadata.DefaultSeverity);
        Assert.False(_rule.Metadata.Fixable);
        Assert.Contains("STRING_AGG", _rule.Metadata.Description);
        Assert.Contains("NVARCHAR(MAX)", _rule.Metadata.Description);
    }

    [Fact]
    public void GetFixes_ReturnsEmpty()
    {
        // Arrange
        var context = RuleTestContext.CreateContext("SELECT STRING_AGG(name, ',') FROM users;", compatLevel: 140);
        var diagnostic = new Diagnostic(
            Range: new TsqlRefine.PluginSdk.Range(new Position(0, 0), new Position(0, 10)),
            Message: "test",
            Code: "string-agg-nvarchar-max"
        );

        // Act
        var fixes = _rule.GetFixes(context, diagnostic).ToArray();

        // Assert
        Assert.Empty(fixes);
    }
}
