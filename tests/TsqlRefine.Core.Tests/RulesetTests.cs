using TsqlRefine.Core.Config;

namespace TsqlRefine.Core.Tests;

public sealed class RulesetTests
{
    [Fact]
    public void CreateSingleRuleWhitelist_EnablesOnlySpecifiedRule()
    {
        var ruleset = Ruleset.CreateSingleRuleWhitelist("rule-a");

        Assert.True(ruleset.IsRuleEnabled("rule-a"));
        Assert.False(ruleset.IsRuleEnabled("rule-b"));
        Assert.False(ruleset.IsRuleEnabled("rule-c"));
    }

    [Fact]
    public void CreateSingleRuleWhitelist_IsCaseInsensitive()
    {
        var ruleset = Ruleset.CreateSingleRuleWhitelist("avoid-select-star");

        Assert.True(ruleset.IsRuleEnabled("avoid-select-star"));
        Assert.True(ruleset.IsRuleEnabled("AVOID-SELECT-STAR"));
        Assert.True(ruleset.IsRuleEnabled("Avoid-Select-Star"));
    }

    [Fact]
    public void DefaultRuleset_EnablesAllRules()
    {
        var ruleset = new Ruleset(null);

        Assert.True(ruleset.IsRuleEnabled("any-rule"));
        Assert.True(ruleset.IsRuleEnabled("another-rule"));
    }

    [Fact]
    public void Ruleset_WithDisabledRule_ReturnsCorrectState()
    {
        var ruleset = new Ruleset(new[]
        {
            new RulesetRule("rule-a", Enabled: true),
            new RulesetRule("rule-b", Enabled: false)
        });

        Assert.True(ruleset.IsRuleEnabled("rule-a"));
        Assert.False(ruleset.IsRuleEnabled("rule-b"));
        Assert.True(ruleset.IsRuleEnabled("rule-c")); // Not in ruleset, default is true
    }
}
