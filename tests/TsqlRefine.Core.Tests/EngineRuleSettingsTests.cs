using TsqlRefine.Core.Config;
using TsqlRefine.Core.Engine;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Core.Tests;

public sealed class EngineRuleSettingsTests
{
    [Fact]
    public void Run_RuleSpecificSettings_ProvidesMatchingSettingsToEachRule()
    {
        var first = new CapturingRule("first");
        var second = new CapturingRule("second");
        var settings = new Dictionary<string, RuleSettings>(StringComparer.OrdinalIgnoreCase)
        {
            ["first"] = CreateSettings(10),
            ["second"] = CreateSettings(20)
        };

        _ = new TsqlRefineEngine([first, second]).Run(
            "lint",
            [new SqlInput("test.sql", "SELECT 1;")],
            new EngineOptions(RuleSettingsByRule: settings));

        Assert.Equal(10, first.CapturedMaximum);
        Assert.Equal(20, second.CapturedMaximum);
    }

    private static RuleSettings CreateSettings(int maximum) =>
        new(new RuleOptions(new Dictionary<string, RuleOptionValue>
        {
            ["max"] = RuleOptionValue.FromInt32(maximum)
        }));

    private sealed class CapturingRule(string id) : IRule
    {
        public RuleMetadata Metadata { get; } = new(id, "Test", "Test", RuleSeverity.Warning, false);
        public int? CapturedMaximum { get; private set; }

        public IEnumerable<Diagnostic> Analyze(RuleContext context)
        {
            if (context.Settings.Options?.TryGetInt32("max", out var maximum) is true)
            {
                CapturedMaximum = maximum;
            }
            return [];
        }

        public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) => [];
    }
}
