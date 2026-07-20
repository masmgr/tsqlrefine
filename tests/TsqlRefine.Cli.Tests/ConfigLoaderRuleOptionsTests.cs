using TsqlRefine.Cli.Services;
using TsqlRefine.Core.Config;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules;

namespace TsqlRefine.Cli.Tests;

public sealed class ConfigLoaderRuleOptionsTests
{
    [Fact]
    public void LoadRuleSettings_ValidIntegerOption_ReturnsTypedSettings()
    {
        var config = CreateConfig(RuleOptionValue.FromInt32(12));

        var settings = ConfigLoader.LoadRuleSettings(config, [new ConfigurableRule()]);

        Assert.True(settings["test-configurable"].Options!.TryGetInt32("max", out var value));
        Assert.Equal(12, value);
    }

    [Fact]
    public void LoadRuleSettings_BuiltinMetricRule_PreservesDescriptorThroughMetadataWrapper()
    {
        var config = new TsqlRefineConfig(Rules: new Dictionary<string, RuleConfig>
        {
            ["max-cyclomatic-complexity"] = new(Options: new Dictionary<string, RuleOptionValue>
            {
                ["max"] = RuleOptionValue.FromInt32(12)
            })
        });

        var settings = ConfigLoader.LoadRuleSettings(config, new BuiltinRuleProvider().GetRules());

        Assert.True(settings["max-cyclomatic-complexity"].Options!.TryGetInt32("max", out var value));
        Assert.Equal(12, value);
    }

    [Fact]
    public void LoadRuleSettings_UnknownOption_ThrowsConfigException()
    {
        var config = new TsqlRefineConfig(Rules: new Dictionary<string, RuleConfig>
        {
            ["test-configurable"] = new(Options: new Dictionary<string, RuleOptionValue>
            {
                ["other"] = RuleOptionValue.FromInt32(12)
            })
        });

        var exception = Assert.Throws<ConfigException>(() =>
            ConfigLoader.LoadRuleSettings(config, [new ConfigurableRule()]));

        Assert.Contains("Unknown option", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LoadRuleSettings_WrongType_ThrowsConfigException()
    {
        var config = CreateConfig(RuleOptionValue.FromString("12"));

        Assert.Throws<ConfigException>(() =>
            ConfigLoader.LoadRuleSettings(config, [new ConfigurableRule()]));
    }

    [Fact]
    public void LoadRuleSettings_OutOfRange_ThrowsConfigException()
    {
        var config = CreateConfig(RuleOptionValue.FromInt32(101));

        Assert.Throws<ConfigException>(() =>
            ConfigLoader.LoadRuleSettings(config, [new ConfigurableRule()]));
    }

    [Fact]
    public void LoadRuleSettings_RuleWithoutDescriptors_ThrowsConfigException()
    {
        var config = new TsqlRefineConfig(Rules: new Dictionary<string, RuleConfig>
        {
            ["plain"] = new(Options: new Dictionary<string, RuleOptionValue>
            {
                ["max"] = RuleOptionValue.FromInt32(10)
            })
        });

        Assert.Throws<ConfigException>(() =>
            ConfigLoader.LoadRuleSettings(config, [new PlainRule()]));
    }

    private static TsqlRefineConfig CreateConfig(RuleOptionValue value) =>
        new(Rules: new Dictionary<string, RuleConfig>
        {
            ["test-configurable"] = new(Options: new Dictionary<string, RuleOptionValue>
            {
                ["max"] = value
            })
        });

    private sealed class ConfigurableRule : PlainRule, IRuleOptionsDescriptorProvider
    {
        public override RuleMetadata Metadata { get; } = new(
            "test-configurable", "Test", "Test", RuleSeverity.Warning, false);

        public IReadOnlyList<RuleOptionDescriptor> OptionDescriptors { get; } =
        [
            new("max", RuleOptionType.Number, "Maximum value.", 1, 100)
        ];
    }

    private class PlainRule : IRule
    {
        public virtual RuleMetadata Metadata { get; } = new(
            "plain", "Test", "Test", RuleSeverity.Warning, false);

        public IEnumerable<Diagnostic> Analyze(RuleContext context) => [];
        public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) => [];
    }
}
