using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Style;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Style;

public sealed class RequireAsForTableAliasRuleTests
{
    private readonly RequireAsForTableAliasRule _rule = new();

    [Fact]
    public void Analyze_TableAliasWithoutAs_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = "SELECT * FROM users u;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
        Assert.Equal("require-as-for-table-alias", diagnostics[0].Code);
        Assert.Contains("AS", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_TableAliasWithAs_ReturnsNoDiagnostic()
    {
        // Arrange
        const string sql = "SELECT * FROM users AS u;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_TableWithoutAlias_ReturnsNoDiagnostic()
    {
        // Arrange
        const string sql = "SELECT * FROM users;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_MultipleTablesWithoutAs_ReturnsMultipleDiagnostics()
    {
        // Arrange
        const string sql = "SELECT * FROM users u JOIN orders o ON u.id = o.user_id;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Equal(2, diagnostics.Length);
    }

    [Fact]
    public void Analyze_MixedAliases_ReturnsOneDiagnostic()
    {
        // Arrange
        const string sql = "SELECT * FROM users AS u JOIN orders o ON u.id = o.user_id;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
        Assert.Equal("require-as-for-table-alias", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_SubqueryAliasWithoutAs_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = "SELECT * FROM (SELECT 1 AS x) sub;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
    }

    [Fact]
    public void Analyze_SubqueryAliasWithAs_ReturnsNoDiagnostic()
    {
        // Arrange
        const string sql = "SELECT * FROM (SELECT 1 AS x) AS sub;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_CTEWithoutAs_ReturnsNoDiagnostic()
    {
        // Arrange - CTEs always use AS for their definition, this is about table references
        const string sql = "WITH cte AS (SELECT 1 AS x) SELECT * FROM cte;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_EmptyInput_ReturnsNoDiagnostic()
    {
        // Arrange
        const string sql = "";
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
        Assert.Equal("require-as-for-table-alias", _rule.Metadata.RuleId);
        Assert.Equal("Style", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Information, _rule.Metadata.DefaultSeverity);
        Assert.True(_rule.Metadata.Fixable);
    }

    private static RuleContext CreateContext(string sql)
    {
        return RuleTestContext.CreateContext(sql);
    }
}
