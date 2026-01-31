using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules;
using TsqlRefine.Rules.Rules.Performance;
using TsqlRefine.Rules.Tests.Helpers;
using Xunit;

namespace TsqlRefine.Rules.Tests.Performance;

public sealed class UtcDatetimeRuleTests
{
    private readonly UtcDatetimeRule _rule = new();



    [Fact]
    public void Analyze_Getdate_ReturnsDiagnostic()
    {
        // Arrange
        var sql = "SELECT GETDATE();";
        var context = RuleTestContext.CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("utc-datetime", diagnostic.Code);
        Assert.Contains("GETDATE", diagnostic.Message);
        Assert.Contains("GETUTCDATE", diagnostic.Message);
    }

    [Fact]
    public void Analyze_Sysdatetime_ReturnsDiagnostic()
    {
        // Arrange
        var sql = "SELECT SYSDATETIME();";
        var context = RuleTestContext.CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("utc-datetime", diagnostic.Code);
        Assert.Contains("SYSDATETIME", diagnostic.Message);
        Assert.Contains("SYSUTCDATETIME", diagnostic.Message);
    }

    [Fact]
    public void Analyze_CurrentTimestamp_ReturnsDiagnostic()
    {
        // Arrange
        var sql = "SELECT CURRENT_TIMESTAMP;";
        var context = RuleTestContext.CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("utc-datetime", diagnostic.Code);
        Assert.Contains("CURRENT_TIMESTAMP", diagnostic.Message);
    }

    [Fact]
    public void Analyze_Sysdatetimeoffset_ReturnsDiagnostic()
    {
        // Arrange
        var sql = "SELECT SYSDATETIMEOFFSET();";
        var context = RuleTestContext.CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("utc-datetime", diagnostic.Code);
        Assert.Contains("SYSDATETIMEOFFSET", diagnostic.Message);
    }

    [Fact]
    public void Analyze_Getutcdate_NoDiagnostic()
    {
        // Arrange
        var sql = "SELECT GETUTCDATE();";
        var context = RuleTestContext.CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_Sysutcdatetime_NoDiagnostic()
    {
        // Arrange
        var sql = "SELECT SYSUTCDATETIME();";
        var context = RuleTestContext.CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_GetdateInInsert_ReturnsDiagnostic()
    {
        // Arrange
        var sql = "INSERT INTO logs (created_at) VALUES (GETDATE());";
        var context = RuleTestContext.CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("utc-datetime", diagnostic.Code);
    }

    [Fact]
    public void Analyze_GetdateInUpdate_ReturnsDiagnostic()
    {
        // Arrange
        var sql = "UPDATE logs SET updated_at = GETDATE() WHERE id = 1;";
        var context = RuleTestContext.CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("utc-datetime", diagnostic.Code);
    }

    [Fact]
    public void Analyze_GetdateInWhere_ReturnsDiagnostic()
    {
        // Arrange
        var sql = "SELECT * FROM logs WHERE created_at > GETDATE() - 7;";
        var context = RuleTestContext.CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("utc-datetime", diagnostic.Code);
    }

    [Fact]
    public void Analyze_MultipleLocalDatetimeFunctions_ReturnsMultipleDiagnostics()
    {
        // Arrange
        var sql = @"
            SELECT GETDATE() AS local_time,
                   SYSDATETIME() AS local_time2,
                   CURRENT_TIMESTAMP AS local_time3;";
        var context = RuleTestContext.CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        Assert.Equal(3, diagnostics.Count);
        Assert.All(diagnostics, d => Assert.Equal("utc-datetime", d.Code));
    }

    [Fact]
    public void Analyze_GetdateCaseInsensitive_ReturnsDiagnostic()
    {
        // Arrange
        var sql = "SELECT getdate();";
        var context = RuleTestContext.CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("utc-datetime", diagnostic.Code);
    }

    [Fact]
    public void GetFixes_ReturnsNoFixes()
    {
        // Arrange
        var sql = "SELECT GETDATE();";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostic = _rule.Analyze(context).First();

        // Act
        var fixes = _rule.GetFixes(context, diagnostic);

        // Assert
        Assert.Empty(fixes);
    }

    [Fact]
    public void Metadata_HasCorrectProperties()
    {
        // Assert
        Assert.Equal("utc-datetime", _rule.Metadata.RuleId);
        Assert.Equal("Performance", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Warning, _rule.Metadata.DefaultSeverity);
        Assert.False(_rule.Metadata.Fixable);
    }
}
