using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Style;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Style;

public sealed class RequireBeginEndForWhileRuleTests
{
    private readonly RequireBeginEndForWhileRule _rule = new();

    [Fact]
    public void Analyze_WhileWithoutBeginEnd_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = @"
            DECLARE @counter INT = 0;
            WHILE @counter < 10
                SET @counter = @counter + 1;
        ";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
        Assert.Equal("require-begin-end-for-while", diagnostics[0].Code);
        Assert.Contains("BEGIN/END", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_WhileWithBeginEnd_ReturnsNoDiagnostic()
    {
        // Arrange
        const string sql = @"
            DECLARE @counter INT = 0;
            WHILE @counter < 10
            BEGIN
                SET @counter = @counter + 1;
            END
        ";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_WhileWithMultipleStatements_WithBeginEnd_ReturnsNoDiagnostic()
    {
        // Arrange
        const string sql = @"
            DECLARE @counter INT = 0;
            WHILE @counter < 10
            BEGIN
                SET @counter = @counter + 1;
                PRINT @counter;
            END
        ";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_MultipleWhileWithoutBeginEnd_ReturnsMultipleDiagnostics()
    {
        // Arrange
        const string sql = @"
            DECLARE @counter INT = 0;
            WHILE @counter < 10
                SET @counter = @counter + 1;

            WHILE @counter > 0
                SET @counter = @counter - 1;
        ";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Equal(2, diagnostics.Length);
    }

    [Fact]
    public void Analyze_NestedWhileWithoutBeginEnd_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = @"
            DECLARE @outer INT = 0;
            DECLARE @inner INT = 0;
            WHILE @outer < 10
            BEGIN
                WHILE @inner < 5
                    SET @inner = @inner + 1;
                SET @outer = @outer + 1;
            END
        ";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
        Assert.Equal("require-begin-end-for-while", diagnostics[0].Code);
    }

    [Fact]
    public void Metadata_HasCorrectProperties()
    {
        // Assert
        Assert.Equal("require-begin-end-for-while", _rule.Metadata.RuleId);
        Assert.Equal("Style", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Warning, _rule.Metadata.DefaultSeverity);
        Assert.False(_rule.Metadata.Fixable);
    }

    private static RuleContext CreateContext(string sql)
    {
        return RuleTestContext.CreateContext(sql);
    }
}
