using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Correctness;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Correctness;

public sealed class AvoidAtatIdentityRuleTests
{
    private readonly AvoidAtatIdentityRule _rule = new();

    [Fact]
    public void Analyze_UsesAtatIdentity_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = "INSERT INTO users (name) VALUES ('John'); SELECT @@IDENTITY;";
        var context = RuleTestContext.CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
        Assert.Equal("avoid-atat-identity", diagnostics[0].Code);
        Assert.Contains("SCOPE_IDENTITY()", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_UsesScopeIdentity_ReturnsNoDiagnostic()
    {
        // Arrange
        const string sql = "INSERT INTO users (name) VALUES ('John'); SELECT SCOPE_IDENTITY();";
        var context = RuleTestContext.CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_CaseInsensitive_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = "SELECT @@identity;";
        var context = RuleTestContext.CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
    }

    [Fact]
    public void Analyze_MultipleOccurrences_ReturnsMultipleDiagnostics()
    {
        // Arrange
        const string sql = "SELECT @@IDENTITY; INSERT INTO t VALUES (1); SELECT @@IDENTITY;";
        var context = RuleTestContext.CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Equal(2, diagnostics.Length);
    }

    [Fact]
    public void Metadata_HasCorrectProperties()
    {
        // Assert
        Assert.Equal("avoid-atat-identity", _rule.Metadata.RuleId);
        Assert.Equal("Correctness", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Warning, _rule.Metadata.DefaultSeverity);
        Assert.False(_rule.Metadata.Fixable);
    }

}
