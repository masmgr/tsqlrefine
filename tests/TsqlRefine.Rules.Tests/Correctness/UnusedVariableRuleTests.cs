using TsqlRefine.Rules.Rules.Correctness;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Correctness;

public sealed class UnusedVariableRuleTests
{
    private readonly UnusedVariableRule _rule = new();

    [Fact]
    public void Analyze_UnreadLocalVariable_ReturnsDiagnostic()
    {
        Assert.Single(_rule.Analyze(RuleTestContext.CreateContext("DECLARE @value int = 1;")));
    }

    [Fact]
    public void Analyze_WriteOnlyVariable_ReturnsDiagnostic()
    {
        const string sql = "DECLARE @value int; SET @value = 1;";

        Assert.Single(_rule.Analyze(RuleTestContext.CreateContext(sql)));
    }

    [Fact]
    public void Analyze_ReadVariable_ReturnsNoDiagnostic()
    {
        const string sql = "DECLARE @value int = 1; SELECT @value;";

        Assert.Empty(_rule.Analyze(RuleTestContext.CreateContext(sql)));
    }

    [Fact]
    public void Analyze_UnreadProcedureParameter_ReturnsDiagnostic()
    {
        const string sql = "CREATE PROCEDURE dbo.P @unused int AS SELECT 1;";

        var diagnostic = Assert.Single(_rule.Analyze(RuleTestContext.CreateContext(sql)));
        Assert.Contains("Parameter", diagnostic.Message);
    }
}
