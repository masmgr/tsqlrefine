using System.Reflection;
using System.Runtime.Loader;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.PluginHost;

public sealed record LoadedPlugin(
    string Path,
    bool Enabled,
    IReadOnlyList<IRuleProvider> Providers,
    string? Error = null,
    AssemblyLoadContext? LoadContext = null
);

public sealed class PluginLoader
{
    public static IReadOnlyList<LoadedPlugin> Load(IEnumerable<PluginDescriptor> plugins)
    {
        var results = new List<LoadedPlugin>();

        foreach (var plugin in plugins ?? Array.Empty<PluginDescriptor>())
        {
            if (!plugin.Enabled)
            {
                results.Add(new LoadedPlugin(plugin.Path, false, Array.Empty<IRuleProvider>()));
                continue;
            }

            try
            {
                var fullPath = Path.GetFullPath(plugin.Path);
                if (!File.Exists(fullPath))
                {
                    results.Add(new LoadedPlugin(plugin.Path, true, Array.Empty<IRuleProvider>(), "File not found."));
                    continue;
                }

                var loadContext = new PluginLoadContext(fullPath);
                var assembly = loadContext.LoadFromAssemblyPath(fullPath);

                var providers = DiscoverProviders(assembly)
                    .Where(p => p.PluginApiVersion == PluginApi.CurrentVersion)
                    .ToArray();

                results.Add(new LoadedPlugin(plugin.Path, true, providers, LoadContext: loadContext));
            }
#pragma warning disable CA1031 // Plugin load failures must not crash the core process.
            catch (Exception ex)
            {
                results.Add(new LoadedPlugin(plugin.Path, true, Array.Empty<IRuleProvider>(), $"{ex.GetType().Name}: {ex.Message}"));
            }
#pragma warning restore CA1031
        }

        return results;
    }

    private static IEnumerable<IRuleProvider> DiscoverProviders(Assembly assembly)
    {
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
                yield return provider;
            }
        }
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
