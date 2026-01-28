using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;

namespace TsqlRefine.PluginHost;

internal sealed class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;
    private readonly string _pluginDirectory;
    private readonly List<string> _nativeDllProbeAttempts = new();

    public IReadOnlyList<string> NativeDllProbeAttempts => _nativeDllProbeAttempts.AsReadOnly();

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
            _nativeDllProbeAttempts.Add($"[Resolver] {libraryPath} (found)");
            return LoadUnmanagedDllFromPath(libraryPath);
        }

        _nativeDllProbeAttempts.Add($"[Resolver] No path resolved for '{unmanagedDllName}'");

        // Fallback: search in plugin directory for common naming patterns
        var candidatePaths = GetUnmanagedDllCandidatePaths(unmanagedDllName);
        foreach (var candidatePath in candidatePaths)
        {
            if (File.Exists(candidatePath))
            {
                _nativeDllProbeAttempts.Add($"{candidatePath} (found)");
                return LoadUnmanagedDllFromPath(candidatePath);
            }
            _nativeDllProbeAttempts.Add($"{candidatePath} (not found)");
        }

        // If not found, let the default resolution continue
        _nativeDllProbeAttempts.Add($"[Default] Attempting default resolution for '{unmanagedDllName}'");
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

        // Get architecture-specific subdirectory names
        var archNames = GetArchitectureNames();

        // Search in multiple locations for each platform-specific name
        foreach (var name in platformSpecificNames.Distinct())
        {
            // 1. runtimes/<rid>/native pattern (most specific)
            var rid = RuntimeInformation.RuntimeIdentifier;
            yield return Path.Combine(_pluginDirectory, "runtimes", rid, "native", name);

            // 2. runtimes/<os>-<arch>/native pattern (common fallback)
            foreach (var archName in archNames)
            {
                var os = GetOSIdentifier();
                yield return Path.Combine(_pluginDirectory, "runtimes", $"{os}-{archName}", "native", name);
            }

            // 3. Architecture-specific subdirectory
            foreach (var archName in archNames)
            {
                yield return Path.Combine(_pluginDirectory, archName, name);
            }

            // 4. Plugin directory root
            yield return Path.Combine(_pluginDirectory, name);

            // 5. Parent directory native folder (for shared native libs)
            var parentDir = Directory.GetParent(_pluginDirectory);
            if (parentDir is not null)
            {
                yield return Path.Combine(parentDir.FullName, "native", name);
            }
        }
    }

    private static string GetOSIdentifier()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return "win";
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return "linux";
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return "osx";
        }
        return "unknown";
    }

    private static IEnumerable<string> GetArchitectureNames()
    {
        var arch = RuntimeInformation.ProcessArchitecture;
        return arch switch
        {
            Architecture.X64 => new[] { "x64", "x86_64" },
            Architecture.X86 => new[] { "x86" },
            Architecture.Arm64 => new[] { "arm64", "aarch64" },
            Architecture.Arm => new[] { "arm" },
            _ => new[] { arch.ToString().ToLowerInvariant() }
        };
    }
}

