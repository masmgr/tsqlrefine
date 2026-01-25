using System.Reflection;
using System.Runtime.Loader;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.PluginHost;

public enum PluginLoadStatus
{
    Success,
    Disabled,
    FileNotFound,
    LoadError,
    VersionMismatch,
    NoProviders
}

public sealed record PluginLoadDiagnostic(
    PluginLoadStatus Status,
    string? Message = null,
    int? ActualApiVersion = null,
    int? ExpectedApiVersion = null,
    string? ExceptionType = null,
    string? StackTrace = null
);

public sealed record LoadedPlugin(
    string Path,
    bool Enabled,
    IReadOnlyList<IRuleProvider> Providers,
    PluginLoadDiagnostic Diagnostic,
    AssemblyLoadContext? LoadContext = null
)
{
    // Legacy error property for backwards compatibility
    public string? Error => Diagnostic.Status == PluginLoadStatus.Success ? null : Diagnostic.Message;
};

public sealed class PluginLoader
{
    public static IReadOnlyList<LoadedPlugin> Load(IEnumerable<PluginDescriptor> plugins)
    {
        var results = new List<LoadedPlugin>();

        foreach (var plugin in plugins ?? Array.Empty<PluginDescriptor>())
        {
            if (!plugin.Enabled)
            {
                results.Add(new LoadedPlugin(
                    plugin.Path,
                    false,
                    Array.Empty<IRuleProvider>(),
                    new PluginLoadDiagnostic(PluginLoadStatus.Disabled, "Plugin is disabled in configuration.")));
                continue;
            }

            try
            {
                var fullPath = Path.GetFullPath(plugin.Path);
                if (!File.Exists(fullPath))
                {
                    results.Add(new LoadedPlugin(
                        plugin.Path,
                        true,
                        Array.Empty<IRuleProvider>(),
                        new PluginLoadDiagnostic(
                            PluginLoadStatus.FileNotFound,
                            $"Plugin file not found: {fullPath}")));
                    continue;
                }

                var loadContext = new PluginLoadContext(fullPath);
                var assembly = loadContext.LoadFromAssemblyPath(fullPath);

                var (compatibleProviders, incompatibleProviders) = DiscoverProviders(assembly);

                // Check for version mismatches
                if (compatibleProviders.Count == 0 && incompatibleProviders.Count > 0)
                {
                    var mismatchedVersion = incompatibleProviders.First().PluginApiVersion;
                    results.Add(new LoadedPlugin(
                        plugin.Path,
                        true,
                        Array.Empty<IRuleProvider>(),
                        new PluginLoadDiagnostic(
                            PluginLoadStatus.VersionMismatch,
                            $"Plugin API version mismatch. Plugin uses version {mismatchedVersion}, but host expects version {PluginApi.CurrentVersion}.",
                            ActualApiVersion: mismatchedVersion,
                            ExpectedApiVersion: PluginApi.CurrentVersion),
                        loadContext));
                    continue;
                }

                if (compatibleProviders.Count == 0)
                {
                    results.Add(new LoadedPlugin(
                        plugin.Path,
                        true,
                        Array.Empty<IRuleProvider>(),
                        new PluginLoadDiagnostic(
                            PluginLoadStatus.NoProviders,
                            "No IRuleProvider implementations found in plugin assembly."),
                        loadContext));
                    continue;
                }

                results.Add(new LoadedPlugin(
                    plugin.Path,
                    true,
                    compatibleProviders,
                    new PluginLoadDiagnostic(
                        PluginLoadStatus.Success,
                        $"Successfully loaded {compatibleProviders.Count} provider(s)."),
                    loadContext));
            }
#pragma warning disable CA1031 // Plugin load failures must not crash the core process.
            catch (Exception ex)
            {
                results.Add(new LoadedPlugin(
                    plugin.Path,
                    true,
                    Array.Empty<IRuleProvider>(),
                    new PluginLoadDiagnostic(
                        PluginLoadStatus.LoadError,
                        ex.Message,
                        ExceptionType: ex.GetType().Name,
                        StackTrace: ex.StackTrace)));
            }
#pragma warning restore CA1031
        }

        return results;
    }

    private static (IReadOnlyList<IRuleProvider> Compatible, IReadOnlyList<IRuleProvider> Incompatible) DiscoverProviders(Assembly assembly)
    {
        var compatible = new List<IRuleProvider>();
        var incompatible = new List<IRuleProvider>();

        foreach (var type in SafeGetTypes(assembly))
        {
            if (type.IsAbstract || type.IsInterface)
            {
                continue;
            }

            if (!typeof(IRuleProvider).IsAssignableFrom(type))
            {
                continue;
            }

            if (type.GetConstructor(Type.EmptyTypes) is null)
            {
                continue;
            }

            IRuleProvider? provider = null;
            try
            {
                provider = Activator.CreateInstance(type) as IRuleProvider;
            }
#pragma warning disable CA1031 // We ignore broken providers during discovery.
            catch
            {
                // ignore broken provider
            }
#pragma warning restore CA1031

            if (provider is not null)
            {
                if (provider.PluginApiVersion == PluginApi.CurrentVersion)
                {
                    compatible.Add(provider);
                }
                else
                {
                    incompatible.Add(provider);
                }
            }
        }

        return (compatible, incompatible);
    }

    private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t is not null)!;
        }
    }
}
