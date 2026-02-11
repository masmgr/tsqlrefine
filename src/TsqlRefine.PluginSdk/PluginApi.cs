namespace TsqlRefine.PluginSdk;

/// <summary>
/// Provides API versioning constants for plugin compatibility.
/// </summary>
/// <remarks>
/// Plugins must declare their API version via <see cref="IRuleProvider.PluginApiVersion"/>.
/// The host validates this version during plugin loading to prevent compatibility mismatches.
/// </remarks>
public static class PluginApi
{
    /// <summary>
    /// The current Plugin API version.
    /// Plugins must set <see cref="IRuleProvider.PluginApiVersion"/> to this value.
    /// </summary>
    public const int CurrentVersion = 2;
}

