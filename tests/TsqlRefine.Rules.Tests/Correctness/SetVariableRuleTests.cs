using TsqlRefine.Rules.Rules.Correctness.Semantic;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Correctness;

public sealed class SetVariableRuleTests
{
    private readonly SetVariableRule _rule = new();



    [Fact]
    public void Analyze_SelectVariableAssignment_NoDiagnostics()
    {
        // Arrange
        var sql = "DECLARE @Count INT; SELECT @Count = COUNT(*) FROM Users;";

        // Act
        var diagnostics = _rule.Analyze(RuleTestContext.CreateContext(sql)).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_SetVariableAssignment_ReturnsDiagnostic()
    {
        // Arrange
        var sql = "DECLARE @Count INT; SET @Count = 10;";

        // Act
        var diagnostics = _rule.Analyze(RuleTestContext.CreateContext(sql)).ToArray();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("semantic-set-variable", diagnostic.Code);
        Assert.Contains("SET", diagnostic.Message);
        Assert.Contains("SELECT", diagnostic.Message);
    }

    [Fact]
    public void Analyze_SetVariableAssignment_HighlightsSetKeyword()
    {
        // Arrange
        var sql = "DECLARE @Count INT; SET @Count = 10;";

        // Act
        var diagnostics = _rule.Analyze(RuleTestContext.CreateContext(sql)).ToArray();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(0, diagnostic.Range.Start.Line);
        Assert.Equal(20, diagnostic.Range.Start.Character);
        Assert.Equal(0, diagnostic.Range.End.Line);
        Assert.Equal(23, diagnostic.Range.End.Character);
    }

    [Fact]
    public void Analyze_SetVariableWithExpression_ReturnsDiagnostic()
    {
        // Arrange
        var sql = "DECLARE @Total DECIMAL; SET @Total = 100 * 1.08;";

        // Act
        var diagnostics = _rule.Analyze(RuleTestContext.CreateContext(sql)).ToArray();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("semantic-set-variable", diagnostic.Code);
    }

    [Fact]
    public void Analyze_MultipleSetStatements_ReturnsMultipleDiagnostics()
    {
        // Arrange
        var sql = @"
DECLARE @A INT, @B INT;
SET @A = 1;
SET @B = 2;";

        // Act
        var diagnostics = _rule.Analyze(RuleTestContext.CreateContext(sql)).ToArray();

        // Assert
        Assert.Equal(2, diagnostics.Length);
        Assert.All(diagnostics, d => Assert.Equal("semantic-set-variable", d.Code));
    }

    [Fact]
    public void Analyze_SetNocountOn_NoDiagnostics()
    {
        // Arrange
        var sql = "SET NOCOUNT ON;";

        // Act
        var diagnostics = _rule.Analyze(RuleTestContext.CreateContext(sql)).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_SetAnsiNullsOn_NoDiagnostics()
    {
        // Arrange
        var sql = "SET ANSI_NULLS ON;";

        // Act
        var diagnostics = _rule.Analyze(RuleTestContext.CreateContext(sql)).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_SetQuotedIdentifierOn_NoDiagnostics()
    {
        // Arrange
        var sql = "SET QUOTED_IDENTIFIER ON;";

        // Act
        var diagnostics = _rule.Analyze(RuleTestContext.CreateContext(sql)).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }
}
