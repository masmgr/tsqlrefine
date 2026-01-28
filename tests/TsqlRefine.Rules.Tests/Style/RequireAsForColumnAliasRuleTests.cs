using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Style;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Style;

public sealed class RequireAsForColumnAliasRuleTests
{
    private readonly RequireAsForColumnAliasRule _rule = new();

    [Fact]
    public void Analyze_ColumnAliasWithoutAs_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = "SELECT id userId FROM users;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
        Assert.Equal("require-as-for-column-alias", diagnostics[0].Code);
        Assert.Contains("AS", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_ColumnAliasWithAs_ReturnsNoDiagnostic()
    {
        // Arrange
        const string sql = "SELECT id AS userId FROM users;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_ColumnWithoutAlias_ReturnsNoDiagnostic()
    {
        // Arrange
        const string sql = "SELECT id FROM users;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_MultipleColumnsWithoutAs_ReturnsMultipleDiagnostics()
    {
        // Arrange
        const string sql = "SELECT id userId, name userName FROM users;";
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
        const string sql = "SELECT id AS userId, name userName FROM users;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
        Assert.Equal("require-as-for-column-alias", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_ExpressionAliasWithoutAs_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = "SELECT COUNT(*) total FROM users;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
    }

    [Fact]
    public void Analyze_ExpressionAliasWithAs_ReturnsNoDiagnostic()
    {
        // Arrange
        const string sql = "SELECT COUNT(*) AS total FROM users;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_SelectStar_ReturnsNoDiagnostic()
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
        Assert.Equal("require-as-for-column-alias", _rule.Metadata.RuleId);
        Assert.Equal("Style", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Information, _rule.Metadata.DefaultSeverity);
        Assert.True(_rule.Metadata.Fixable);
    }

    private static RuleContext CreateContext(string sql)
    {
        return RuleTestContext.CreateContext(sql);
    }
}
