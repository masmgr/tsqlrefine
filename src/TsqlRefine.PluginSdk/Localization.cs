using System.Globalization;
using System.Resources;

namespace TsqlRefine.PluginSdk;

/// <summary>Convenience provider for embedded .resx resources in a plugin assembly.</summary>
public class ResourceManagerDiagnosticLocalizationProvider : IDiagnosticLocalizationProvider
{
    private readonly ResourceManager _resourceManager;

    /// <summary>Creates a provider for the specified resource base name and assembly.</summary>
    public ResourceManagerDiagnosticLocalizationProvider(
        string name,
        string resourceBaseName,
        System.Reflection.Assembly assembly)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceBaseName);
        ArgumentNullException.ThrowIfNull(assembly);
        Name = name;
        _resourceManager = new ResourceManager(resourceBaseName, assembly);
    }

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public string? GetString(string key, CultureInfo culture)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(culture);
        try
        {
            return _resourceManager.GetString(key, culture);
        }
        catch (MissingManifestResourceException)
        {
            return null;
        }
    }
}
