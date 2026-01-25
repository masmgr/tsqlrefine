using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;

namespace TsqlRefine.PluginHost;

internal sealed class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;
    private readonly string _pluginDirectory;

    public PluginLoadContext(string pluginMainAssemblyPath)
        : base(isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(pluginMainAssemblyPath);
        _pluginDirectory = Path.GetDirectoryName(pluginMainAssemblyPath)
            ?? throw new ArgumentException("Plugin path must have a directory.", nameof(pluginMainAssemblyPath));
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var path = _resolver.ResolveAssemblyToPath(assemblyName);
        if (path is null)
        {
            return null;
        }

        return LoadFromAssemblyPath(path);
    }

    protected override nint LoadUnmanagedDll(string unmanagedDllName)
    {
        // Try to resolve using the dependency resolver first
        var libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        if (libraryPath is not null)
        {
            return LoadUnmanagedDllFromPath(libraryPath);
        }

        // Fallback: search in plugin directory for common naming patterns
        var candidatePaths = GetUnmanagedDllCandidatePaths(unmanagedDllName);
        foreach (var candidatePath in candidatePaths)
        {
            if (File.Exists(candidatePath))
            {
                return LoadUnmanagedDllFromPath(candidatePath);
            }
        }

        // If not found, let the default resolution continue
        return base.LoadUnmanagedDll(unmanagedDllName);
    }

    private IEnumerable<string> GetUnmanagedDllCandidatePaths(string unmanagedDllName)
    {
        // Handle platform-specific naming conventions
        var platformSpecificNames = new List<string> { unmanagedDllName };

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            if (!unmanagedDllName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                platformSpecificNames.Add($"{unmanagedDllName}.dll");
            }
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            if (!unmanagedDllName.StartsWith("lib", StringComparison.Ordinal))
            {
                platformSpecificNames.Add($"lib{unmanagedDllName}");
            }
            if (!unmanagedDllName.EndsWith(".so", StringComparison.OrdinalIgnoreCase))
            {
                platformSpecificNames.Add($"{unmanagedDllName}.so");
                platformSpecificNames.Add($"lib{unmanagedDllName}.so");
            }
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            if (!unmanagedDllName.StartsWith("lib", StringComparison.Ordinal))
            {
                platformSpecificNames.Add($"lib{unmanagedDllName}");
            }
            if (!unmanagedDllName.EndsWith(".dylib", StringComparison.OrdinalIgnoreCase))
            {
                platformSpecificNames.Add($"{unmanagedDllName}.dylib");
                platformSpecificNames.Add($"lib{unmanagedDllName}.dylib");
            }
        }

        // Search in plugin directory and runtimes subdirectory
        foreach (var name in platformSpecificNames.Distinct())
        {
            yield return Path.Combine(_pluginDirectory, name);

            // Also check runtimes/<rid>/native pattern
            var rid = RuntimeInformation.RuntimeIdentifier;
            var runtimeNativePath = Path.Combine(_pluginDirectory, "runtimes", rid, "native", name);
            yield return runtimeNativePath;
        }
    }
}

