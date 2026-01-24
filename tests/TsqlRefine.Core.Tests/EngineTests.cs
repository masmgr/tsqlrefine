using TsqlRefine.Core.Engine;
using TsqlRefine.Rules;

namespace TsqlRefine.Core.Tests;

public sealed class EngineTests
{
    [Fact]
    public void Run_WhenViolation_ReturnsDiagnostics()
    {
        var rules = new BuiltinRuleProvider().GetRules();
        var engine = new TsqlRefineEngine(rules);

        var result = engine.Run(
            command: "lint",
            inputs: new[] { new SqlInput("a.sql", "select * from t;") },
            options: new EngineOptions()
        );

        Assert.Single(result.Files);
        Assert.NotEmpty(result.Files[0].Diagnostics);
    }
}

