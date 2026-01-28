using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Style;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Style;

public sealed class AvoidMagicConvertStyleForDatetimeRuleTests
{
    private readonly AvoidMagicConvertStyleForDatetimeRule _rule = new();

    [Fact]
    public void Analyze_ConvertWithStyleNumber_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = "SELECT CONVERT(VARCHAR, GETDATE(), 101);";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
        Assert.Equal("avoid-magic-convert-style-for-datetime", diagnostics[0].Code);
        Assert.Contains("101", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_ConvertToDatetimeWithStyle_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = "SELECT CONVERT(DATETIME, '2023-01-01', 120);";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
        Assert.Equal("avoid-magic-convert-style-for-datetime", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_ConvertWithoutStyle_ReturnsNoDiagnostic()
    {
        // Arrange
        const string sql = "SELECT CONVERT(VARCHAR, GETDATE());";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_ConvertNonDatetimeType_ReturnsNoDiagnostic()
    {
        // Arrange
        const string sql = "SELECT CONVERT(INT, '123', 1);";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_FormatFunction_ReturnsNoDiagnostic()
    {
        // Arrange
        const string sql = "SELECT FORMAT(GETDATE(), 'yyyy-MM-dd');";
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
        Assert.Equal("avoid-magic-convert-style-for-datetime", _rule.Metadata.RuleId);
        Assert.Equal("Style", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Information, _rule.Metadata.DefaultSeverity);
        Assert.False(_rule.Metadata.Fixable);
    }

    private static RuleContext CreateContext(string sql)
    {
        return RuleTestContext.CreateContext(sql);
    }
}
