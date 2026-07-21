using TsqlRefine.Cli.Services;
using TsqlRefine.Core.Engine;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Cli.Tests;

public sealed class BaselineAnalysisFailureTests
{
    [Fact]
    public void IsAnalysisFailure_RuleException_ReturnsTrue()
    {
        var position = new Position(0, 0);
        var diagnostic = new Diagnostic(
            Range: new TsqlRefine.PluginSdk.Range(position, position),
            Message: "Rule crashed.",
            Severity: DiagnosticSeverity.Error,
            Code: TsqlRefineEngine.RuleExceptionCode,
            Data: new DiagnosticData(TsqlRefineEngine.RuleExceptionCode, "Internal", false));

        Assert.True(BaselineStore.IsAnalysisFailure(diagnostic));
    }
}
