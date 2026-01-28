using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Style;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Style;

public sealed class SchemaQualifyRuleTests
{
    private readonly SchemaQualifyRule _rule = new();

    private static RuleContext CreateContext(string sql)
    {
        return RuleTestContext.CreateContext(sql);
    }

    [Fact]
    public void Analyze_TableWithSchema_NoDiagnostics()
    {
        // Arrange
        var sql = "SELECT * FROM dbo.Users;";

        // Act
        var diagnostics = _rule.Analyze(CreateContext(sql)).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_TableWithoutSchema_ReturnsDiagnostic()
    {
        // Arrange
        var sql = "SELECT * FROM Users;";

        // Act
        var diagnostics = _rule.Analyze(CreateContext(sql)).ToArray();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("semantic/schema-qualify", diagnostic.Code);
        Assert.Contains("schema", diagnostic.Message);
    }

    [Fact]
    public void Analyze_TempTable_NoDiagnostics()
    {
        // Arrange
        var sql = "SELECT * FROM #TempUsers;";

        // Act
        var diagnostics = _rule.Analyze(CreateContext(sql)).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_GlobalTempTable_NoDiagnostics()
    {
        // Arrange
        var sql = "SELECT * FROM ##GlobalTemp;";

        // Act
        var diagnostics = _rule.Analyze(CreateContext(sql)).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_TableVariable_NoDiagnostics()
    {
        // Arrange
        var sql = "DECLARE @Users TABLE (Id INT); SELECT * FROM @Users;";

        // Act
        var diagnostics = _rule.Analyze(CreateContext(sql)).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_SystemTable_NoDiagnostics()
    {
        // Arrange
        var sql = "SELECT * FROM sys.tables;";

        // Act
        var diagnostics = _rule.Analyze(CreateContext(sql)).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_MultipleTablesWithoutSchema_ReturnsMultipleDiagnostics()
    {
        // Arrange
        var sql = "SELECT * FROM Users JOIN Orders ON Users.Id = Orders.UserId;";

        // Act
        var diagnostics = _rule.Analyze(CreateContext(sql)).ToArray();

        // Assert
        Assert.Equal(2, diagnostics.Length);
        Assert.All(diagnostics, d => Assert.Equal("semantic/schema-qualify", d.Code));
    }

    [Fact]
    public void Analyze_JoinWithMixedSchemaQualification_ReturnsDiagnosticForUnqualified()
    {
        // Arrange
        var sql = "SELECT * FROM dbo.Users JOIN Orders ON Users.Id = Orders.UserId;";

        // Act
        var diagnostics = _rule.Analyze(CreateContext(sql)).ToArray();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("semantic/schema-qualify", diagnostic.Code);
        Assert.Contains("Orders", diagnostic.Message);
    }
}
