using TsqlRefine.Core.Config;
using TsqlRefine.Core.Engine;
using TsqlRefine.PluginSdk;
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

    [Fact]
    public void Run_WithSeverityOverride_UseOverriddenSeverity()
    {
        var rules = new BuiltinRuleProvider().GetRules();
        var engine = new TsqlRefineEngine(rules);

        // avoid-select-star defaults to Warning; override to Error
        var ruleset = new Ruleset([new RulesetRule("avoid-select-star", Severity: "error")]);

        var result = engine.Run(
            command: "lint",
            inputs: [new SqlInput("a.sql", "SELECT * FROM t;")],
            options: new EngineOptions(Ruleset: ruleset)
        );

        var diag = Assert.Single(result.Files[0].Diagnostics,
            d => d.Code == "avoid-select-star");
        Assert.Equal(DiagnosticSeverity.Error, diag.Severity);
    }

    [Fact]
    public void Run_WithRuleDisabledViaNone_ExcludesRule()
    {
        var rules = new BuiltinRuleProvider().GetRules();
        var engine = new TsqlRefineEngine(rules);

        // Disable avoid-select-star via severity "none"
        var ruleset = new Ruleset([new RulesetRule("avoid-select-star", Severity: "none")]);

        var result = engine.Run(
            command: "lint",
            inputs: [new SqlInput("a.sql", "SELECT * FROM t;")],
            options: new EngineOptions(Ruleset: ruleset)
        );

        Assert.DoesNotContain(result.Files[0].Diagnostics,
            d => d.Code == "avoid-select-star");
    }

    [Fact]
    public void Run_WithOverridesFromConfig_AppliesMergedRuleset()
    {
        var rules = new BuiltinRuleProvider().GetRules();
        var engine = new TsqlRefineEngine(rules);

        // Base: all rules enabled. Override: change avoid-select-star to error
        var baseRuleset = Ruleset.Empty;
        var merged = baseRuleset.WithOverrides(new Dictionary<string, string>
        {
            ["avoid-select-star"] = "error"
        });

        var result = engine.Run(
            command: "lint",
            inputs: [new SqlInput("a.sql", "SELECT * FROM t;")],
            options: new EngineOptions(Ruleset: merged)
        );

        var diag = Assert.Single(result.Files[0].Diagnostics,
            d => d.Code == "avoid-select-star");
        Assert.Equal(DiagnosticSeverity.Error, diag.Severity);
    }
}

