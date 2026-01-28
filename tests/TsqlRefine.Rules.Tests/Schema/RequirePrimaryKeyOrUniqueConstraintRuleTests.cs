using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Schema;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Schema;

public sealed class RequirePrimaryKeyOrUniqueConstraintRuleTests
{
    private readonly RequirePrimaryKeyOrUniqueConstraintRule _rule = new();

    [Fact]
    public void Analyze_TableWithoutPrimaryKeyOrUnique_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = "CREATE TABLE dbo.Users (id INT, name VARCHAR(100));";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
        Assert.Equal("require-primary-key-or-unique-constraint", diagnostics[0].Code);
        Assert.Contains("PRIMARY KEY or UNIQUE constraint", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_TableWithPrimaryKey_ReturnsNoDiagnostic()
    {
        // Arrange
        const string sql = "CREATE TABLE dbo.Users (id INT PRIMARY KEY, name VARCHAR(100));";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_TableWithTableLevelPrimaryKey_ReturnsNoDiagnostic()
    {
        // Arrange
        const string sql = @"
            CREATE TABLE dbo.Users (
                id INT,
                name VARCHAR(100),
                CONSTRAINT PK_Users PRIMARY KEY (id)
            );
        ";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_TableWithUniqueConstraint_ReturnsNoDiagnostic()
    {
        // Arrange
        const string sql = "CREATE TABLE dbo.Users (id INT UNIQUE, name VARCHAR(100));";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_TableWithTableLevelUniqueConstraint_ReturnsNoDiagnostic()
    {
        // Arrange
        const string sql = @"
            CREATE TABLE dbo.Users (
                id INT,
                name VARCHAR(100),
                CONSTRAINT UQ_Users_Id UNIQUE (id)
            );
        ";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_MultipleTablesWithoutConstraints_ReturnsMultipleDiagnostics()
    {
        // Arrange
        const string sql = @"
            CREATE TABLE dbo.Users (id INT, name VARCHAR(100));
            CREATE TABLE dbo.Orders (id INT, userId INT);
        ";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Equal(2, diagnostics.Length);
    }

    [Fact]
    public void Metadata_HasCorrectProperties()
    {
        // Assert
        Assert.Equal("require-primary-key-or-unique-constraint", _rule.Metadata.RuleId);
        Assert.Equal("Schema", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Warning, _rule.Metadata.DefaultSeverity);
        Assert.False(_rule.Metadata.Fixable);
    }

    private static RuleContext CreateContext(string sql)
    {
        return RuleTestContext.CreateContext(sql);
    }
}
