namespace TsqlRefine.PluginHost;

/// <summary>
/// Descriptor for a plugin assembly to be loaded.
/// </summary>
/// <param name="Path">The file system path to the plugin DLL.</param>
/// <param name="Enabled">Whether the plugin should be loaded. Default is true.</param>
/// <param name="ResolvedFullPath">Pre-resolved absolute path from search-path resolution.
/// When set, <see cref="PluginLoader"/> verifies that it is contained in one of the trusted
/// resolved-path directories supplied by the caller.</param>
public sealed record PluginDescriptor(string Path, bool Enabled = true, string? ResolvedFullPath = null);

