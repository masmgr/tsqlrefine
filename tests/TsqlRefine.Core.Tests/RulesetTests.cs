using TsqlRefine.Core.Config;
using TsqlRefine.PluginSdk;

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
        var ruleset = new Ruleset(
        [
            new RulesetRule("rule-a", Severity: "inherit"),
            new RulesetRule("rule-b", Severity: "none")
        ]);

        Assert.True(ruleset.IsRuleEnabled("rule-a"));
        Assert.False(ruleset.IsRuleEnabled("rule-b"));
        Assert.True(ruleset.IsRuleEnabled("rule-c")); // Not in ruleset, default is true
    }

    // --- ParseSeverityLevel tests ---

    [Theory]
    [InlineData(null, RuleSeverityLevel.Inherit)]
    [InlineData("", RuleSeverityLevel.Inherit)]
    [InlineData("none", RuleSeverityLevel.None)]
    [InlineData("inherit", RuleSeverityLevel.Inherit)]
    [InlineData("error", RuleSeverityLevel.Error)]
    [InlineData("warning", RuleSeverityLevel.Warning)]
    [InlineData("info", RuleSeverityLevel.Info)]
    public void ParseSeverityLevel_ValidValues(string? input, RuleSeverityLevel expected)
    {
        Assert.Equal(expected, Ruleset.ParseSeverityLevel(input));
    }

    [Theory]
    [InlineData("Error")]
    [InlineData("WARNING")]
    [InlineData("Info")]
    [InlineData("NONE")]
    [InlineData("Inherit")]
    public void ParseSeverityLevel_IsCaseInsensitive(string input)
    {
        // Should not throw
        Ruleset.ParseSeverityLevel(input);
    }

    [Theory]
    [InlineData("critical")]
    [InlineData("high")]
    [InlineData("true")]
    public void ParseSeverityLevel_InvalidValue_Throws(string input)
    {
        Assert.Throws<ConfigValidationException>(() => Ruleset.ParseSeverityLevel(input));
    }

    // --- Severity field in RulesetRule ---

    [Fact]
    public void Ruleset_WithSeverityField_ControlsEnablement()
    {
        var ruleset = new Ruleset(
        [
            new RulesetRule("rule-a", Severity: "error"),
            new RulesetRule("rule-b", Severity: "none"),
            new RulesetRule("rule-c", Severity: "inherit"),
            new RulesetRule("rule-d", Severity: "warning"),
            new RulesetRule("rule-e", Severity: "info")
        ]);

        Assert.True(ruleset.IsRuleEnabled("rule-a"));
        Assert.False(ruleset.IsRuleEnabled("rule-b"));
        Assert.True(ruleset.IsRuleEnabled("rule-c"));
        Assert.True(ruleset.IsRuleEnabled("rule-d"));
        Assert.True(ruleset.IsRuleEnabled("rule-e"));
    }

    [Fact]
    public void Ruleset_SeverityNone_DisablesRule()
    {
        var ruleset = new Ruleset(
        [
            new RulesetRule("rule-a", Severity: "none")
        ]);

        Assert.False(ruleset.IsRuleEnabled("rule-a"));
    }

    // --- GetRuleSeverityLevel tests ---

    [Fact]
    public void GetRuleSeverityLevel_ReturnsConfiguredLevel()
    {
        var ruleset = new Ruleset(
        [
            new RulesetRule("rule-a", Severity: "error"),
            new RulesetRule("rule-b", Severity: "warning"),
            new RulesetRule("rule-c", Severity: "info"),
            new RulesetRule("rule-d", Severity: "inherit"),
            new RulesetRule("rule-e", Severity: "none")
        ]);

        Assert.Equal(RuleSeverityLevel.Error, ruleset.GetRuleSeverityLevel("rule-a"));
        Assert.Equal(RuleSeverityLevel.Warning, ruleset.GetRuleSeverityLevel("rule-b"));
        Assert.Equal(RuleSeverityLevel.Info, ruleset.GetRuleSeverityLevel("rule-c"));
        Assert.Equal(RuleSeverityLevel.Inherit, ruleset.GetRuleSeverityLevel("rule-d"));
        Assert.Equal(RuleSeverityLevel.None, ruleset.GetRuleSeverityLevel("rule-e"));
    }

    [Fact]
    public void GetRuleSeverityLevel_UnknownRule_ReturnsInherit()
    {
        var ruleset = new Ruleset([new RulesetRule("rule-a", Severity: "error")]);

        Assert.Equal(RuleSeverityLevel.Inherit, ruleset.GetRuleSeverityLevel("unknown-rule"));
    }

    // --- GetSeverityOverride tests ---

    [Fact]
    public void GetSeverityOverride_ReturnsOverrideForExplicitSeverity()
    {
        var ruleset = new Ruleset(
        [
            new RulesetRule("rule-a", Severity: "error"),
            new RulesetRule("rule-b", Severity: "warning"),
            new RulesetRule("rule-c", Severity: "info")
        ]);

        Assert.Equal(DiagnosticSeverity.Error, ruleset.GetSeverityOverride("rule-a"));
        Assert.Equal(DiagnosticSeverity.Warning, ruleset.GetSeverityOverride("rule-b"));
        Assert.Equal(DiagnosticSeverity.Information, ruleset.GetSeverityOverride("rule-c"));
    }

    [Fact]
    public void GetSeverityOverride_ReturnsNull_ForInheritAndNone()
    {
        var ruleset = new Ruleset(
        [
            new RulesetRule("rule-a", Severity: "inherit"),
            new RulesetRule("rule-b", Severity: "none")
        ]);

        Assert.Null(ruleset.GetSeverityOverride("rule-a"));
        Assert.Null(ruleset.GetSeverityOverride("rule-b"));
    }

    [Fact]
    public void GetSeverityOverride_ReturnsNull_ForUnknownRule()
    {
        var ruleset = new Ruleset([new RulesetRule("rule-a", Severity: "error")]);

        Assert.Null(ruleset.GetSeverityOverride("unknown-rule"));
    }

    // --- WithOverrides tests ---

    [Fact]
    public void WithOverrides_AppliesEnablementOverride()
    {
        var baseRuleset = new Ruleset([new RulesetRule("rule-a", Severity: "error")]);

        var merged = baseRuleset.WithOverrides(new Dictionary<string, string>
        {
            ["rule-a"] = "none"
        });

        Assert.False(merged.IsRuleEnabled("rule-a"));
    }

    [Fact]
    public void WithOverrides_AppliesSeverityOverride()
    {
        var baseRuleset = new Ruleset([new RulesetRule("rule-a", Severity: "warning")]);

        var merged = baseRuleset.WithOverrides(new Dictionary<string, string>
        {
            ["rule-a"] = "error"
        });

        Assert.Equal(DiagnosticSeverity.Error, merged.GetSeverityOverride("rule-a"));
    }

    [Fact]
    public void WithOverrides_PreservesBaseRules()
    {
        var baseRuleset = new Ruleset(
        [
            new RulesetRule("rule-a", Severity: "warning"),
            new RulesetRule("rule-b", Severity: "error")
        ]);

        var merged = baseRuleset.WithOverrides(new Dictionary<string, string>
        {
            ["rule-a"] = "info"
        });

        // rule-a overridden
        Assert.Equal(DiagnosticSeverity.Information, merged.GetSeverityOverride("rule-a"));
        // rule-b preserved
        Assert.Equal(DiagnosticSeverity.Error, merged.GetSeverityOverride("rule-b"));
    }

    [Fact]
    public void WithOverrides_AddsNewRules()
    {
        var baseRuleset = Ruleset.Empty;

        var merged = baseRuleset.WithOverrides(new Dictionary<string, string>
        {
            ["new-rule"] = "error"
        });

        Assert.True(merged.IsRuleEnabled("new-rule"));
        Assert.Equal(DiagnosticSeverity.Error, merged.GetSeverityOverride("new-rule"));
    }

    [Fact]
    public void WithOverrides_CanDisableRuleFromPreset()
    {
        var baseRuleset = new Ruleset([new RulesetRule("rule-a")]);

        var merged = baseRuleset.WithOverrides(new Dictionary<string, string>
        {
            ["rule-a"] = "none"
        });

        Assert.False(merged.IsRuleEnabled("rule-a"));
    }

    [Fact]
    public void WithOverrides_OnEmptyRuleset_Works()
    {
        var merged = Ruleset.Empty.WithOverrides(new Dictionary<string, string>
        {
            ["rule-a"] = "error",
            ["rule-b"] = "none"
        });

        Assert.True(merged.IsRuleEnabled("rule-a"));
        Assert.False(merged.IsRuleEnabled("rule-b"));
        Assert.Equal(DiagnosticSeverity.Error, merged.GetSeverityOverride("rule-a"));
    }
}
