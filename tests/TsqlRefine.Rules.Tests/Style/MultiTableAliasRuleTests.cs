using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Style.Semantic;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Style;

public sealed class MultiTableAliasRuleTests
{
    private readonly MultiTableAliasRule _rule = new();

    private static RuleContext CreateContext(string sql)
    {
        return RuleTestContext.CreateContext(sql);
    }

    [Fact]
    public void Analyze_SingleTable_NoDiagnostics()
    {
        // Arrange
        var sql = "SELECT Id, Name FROM Users;";

        // Act
        var diagnostics = _rule.Analyze(CreateContext(sql)).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_JoinWithQualifiedColumns_NoDiagnostics()
    {
        // Arrange
        var sql = "SELECT u.Id, o.Total FROM Users u JOIN Orders o ON u.Id = o.UserId;";

        // Act
        var diagnostics = _rule.Analyze(CreateContext(sql)).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_JoinWithUnqualifiedColumn_ReturnsDiagnostic()
    {
        // Arrange
        var sql = "SELECT Id FROM Users u JOIN Orders o ON u.Id = o.UserId;";

        // Act
        var diagnostics = _rule.Analyze(CreateContext(sql)).ToArray();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("semantic/multi-table-alias", diagnostic.Code);
        Assert.Contains("qualified", diagnostic.Message);
    }

    [Fact]
    public void Analyze_JoinWithMixedQualification_ReturnsDiagnosticForUnqualified()
    {
        // Arrange
        var sql = "SELECT u.Id, Name FROM Users u JOIN Orders o ON u.Id = o.UserId;";

        // Act
        var diagnostics = _rule.Analyze(CreateContext(sql)).ToArray();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("semantic/multi-table-alias", diagnostic.Code);
        Assert.Contains("Name", diagnostic.Message);
    }

    [Fact]
    public void Analyze_MultipleJoinsWithUnqualifiedColumns_ReturnsMultipleDiagnostics()
    {
        // Arrange
        var sql = "SELECT Id, Name FROM Users u JOIN Orders o ON u.Id = o.UserId JOIN Products p ON o.ProductId = p.Id;";

        // Act
        var diagnostics = _rule.Analyze(CreateContext(sql)).ToArray();

        // Assert
        Assert.Equal(2, diagnostics.Length);
        Assert.All(diagnostics, d => Assert.Equal("semantic/multi-table-alias", d.Code));
    }

    [Fact]
    public void Analyze_SelectStarWithJoin_NoDiagnostics()
    {
        // Arrange - SELECT * is a special case, not a column reference
        var sql = "SELECT * FROM Users u JOIN Orders o ON u.Id = o.UserId;";

        // Act
        var diagnostics = _rule.Analyze(CreateContext(sql)).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_WhereClauseWithUnqualifiedColumn_ReturnsDiagnostic()
    {
        // Arrange
        var sql = "SELECT u.Id FROM Users u JOIN Orders o ON u.Id = o.UserId WHERE Status = 'Active';";

        // Act
        var diagnostics = _rule.Analyze(CreateContext(sql)).ToArray();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("semantic/multi-table-alias", diagnostic.Code);
        Assert.Contains("Status", diagnostic.Message);
    }
}
