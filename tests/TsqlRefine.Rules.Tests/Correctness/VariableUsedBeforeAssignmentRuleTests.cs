using TsqlRefine.Rules.Rules.Correctness;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Correctness;

public sealed class VariableUsedBeforeAssignmentRuleTests
{
    private readonly VariableUsedBeforeAssignmentRule _rule = new();

    [Fact]
    public void Analyze_UnassignedVariableRead_ReturnsDiagnostic()
    {
        var context = RuleTestContext.CreateContext("DECLARE @value int; SELECT @value;");

        Assert.Single(_rule.Analyze(context));
    }

    [Fact]
    public void Analyze_InitializedVariable_ReturnsNoDiagnostic()
    {
        var context = RuleTestContext.CreateContext("DECLARE @value int = 1; SELECT @value;");

        Assert.Empty(_rule.Analyze(context));
    }

    [Fact]
    public void Analyze_AssignedOnOnlyOneBranch_ReturnsDiagnostic()
    {
        const string sql = "DECLARE @value int; IF @flag = 1 SET @value = 1; SELECT @value;";

        Assert.Single(_rule.Analyze(RuleTestContext.CreateContext(sql)));
    }

    [Fact]
    public void Analyze_AssignedOnBothBranches_ReturnsNoDiagnostic()
    {
        const string sql = "DECLARE @value int; IF @flag = 1 SET @value = 1; ELSE SET @value = 2; SELECT @value;";

        Assert.Empty(_rule.Analyze(RuleTestContext.CreateContext(sql)));
    }

    [Fact]
    public void Analyze_ProcedureParameter_IsInitiallyAssigned()
    {
        const string sql = "CREATE PROCEDURE dbo.P @value int AS SELECT @value;";

        Assert.Empty(_rule.Analyze(RuleTestContext.CreateContext(sql)));
    }
}
