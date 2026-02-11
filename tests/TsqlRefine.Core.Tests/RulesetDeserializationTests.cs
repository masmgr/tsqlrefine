using System.Text.Json;
using TsqlRefine.Core.Config;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Core.Tests;

public sealed class RulesetDeserializationTests
{
    [Fact]
    public void Deserialize_WithSeverityField()
    {
        var json = """
        {
            "rules": [
                { "id": "rule-a", "severity": "error" },
                { "id": "rule-b", "severity": "none" },
                { "id": "rule-c", "severity": "inherit" }
            ]
        }
        """;

        var ruleset = JsonSerializer.Deserialize<Ruleset>(json, JsonDefaults.Options)!;

        Assert.True(ruleset.IsRuleEnabled("rule-a"));
        Assert.False(ruleset.IsRuleEnabled("rule-b"));
        Assert.True(ruleset.IsRuleEnabled("rule-c"));
        Assert.Equal(DiagnosticSeverity.Error, ruleset.GetSeverityOverride("rule-a"));
        Assert.Null(ruleset.GetSeverityOverride("rule-c"));
    }

    [Fact]
    public void Deserialize_WithoutSeverity_DefaultsToInherit()
    {
        var json = """
        {
            "rules": [
                { "id": "rule-a" }
            ]
        }
        """;

        var ruleset = JsonSerializer.Deserialize<Ruleset>(json, JsonDefaults.Options)!;

        Assert.True(ruleset.IsRuleEnabled("rule-a"));
        Assert.Equal(RuleSeverityLevel.Inherit, ruleset.GetRuleSeverityLevel("rule-a"));
    }

    [Fact]
    public void Deserialize_AllSeverityValues()
    {
        var json = """
        {
            "rules": [
                { "id": "r1", "severity": "error" },
                { "id": "r2", "severity": "warning" },
                { "id": "r3", "severity": "info" },
                { "id": "r4", "severity": "inherit" },
                { "id": "r5", "severity": "none" }
            ]
        }
        """;

        var ruleset = JsonSerializer.Deserialize<Ruleset>(json, JsonDefaults.Options)!;

        Assert.Equal(RuleSeverityLevel.Error, ruleset.GetRuleSeverityLevel("r1"));
        Assert.Equal(RuleSeverityLevel.Warning, ruleset.GetRuleSeverityLevel("r2"));
        Assert.Equal(RuleSeverityLevel.Info, ruleset.GetRuleSeverityLevel("r3"));
        Assert.Equal(RuleSeverityLevel.Inherit, ruleset.GetRuleSeverityLevel("r4"));
        Assert.Equal(RuleSeverityLevel.None, ruleset.GetRuleSeverityLevel("r5"));
    }
}
