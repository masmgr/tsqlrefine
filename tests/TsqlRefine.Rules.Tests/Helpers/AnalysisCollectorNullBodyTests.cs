using TsqlRefine.Rules.Helpers.ControlFlow;
using TsqlRefine.Rules.Helpers.Metrics;

namespace TsqlRefine.Rules.Tests.Helpers;

public sealed class AnalysisCollectorNullBodyTests
{
    private const string EmptyProcedure = "CREATE PROCEDURE dbo.EmptyProcedure AS";

    [Fact]
    public void ControlFlowScopes_EmptyProcedure_ReturnsNoScopes()
    {
        var context = RuleTestContext.CreateContext(EmptyProcedure);

        var scopes = ControlFlowScopeCollector.Collect(context.Ast.Fragment!);

        Assert.Empty(scopes);
    }

    [Fact]
    public void SqlMetrics_EmptyProcedure_ReturnsNoMetrics()
    {
        var context = RuleTestContext.CreateContext(EmptyProcedure);

        var metrics = SqlMetricsCollector.Collect(context.Ast.Fragment!);

        Assert.Empty(metrics);
    }
}
