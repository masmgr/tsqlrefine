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

/// <summary>
/// Assembly-level attribute declaring the Plugin API version a plugin was built against.
/// This allows the host to verify compatibility before instantiating any types.
/// </summary>
/// <param name="version">The API version (should match <see cref="PluginApi.CurrentVersion"/>).</param>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class PluginApiVersionAttribute(int version) : Attribute
{
    /// <summary>
    /// Gets the Plugin API version declared by this assembly.
    /// </summary>
    public int Version { get; } = version;
}

