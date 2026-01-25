using TsqlRefine.PluginSdk;

namespace CustomRule;

/// <summary>
/// Plugin provider that exposes custom rules to tsqlrefine.
/// </summary>
public sealed class CustomRuleProvider : IRuleProvider
{
    /// <summary>
    /// Gets the name of this plugin provider.
    /// </summary>
    public string Name => "custom-rule-sample";

    /// <summary>
    /// Gets the plugin API version this provider targets.
    /// Must match TsqlRefine.PluginSdk.PluginApi.CurrentVersion.
    /// </summary>
    public int PluginApiVersion => PluginApi.CurrentVersion;

    /// <summary>
    /// Returns the list of custom rules provided by this plugin.
    /// </summary>
    public IReadOnlyList<IRule> GetRules() =>
        new IRule[]
        {
            new NoMagicNumbersRule()
        };
}
