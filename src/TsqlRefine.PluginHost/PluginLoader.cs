using System.Reflection;
using System.Runtime.Loader;
using System.Text.RegularExpressions;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.PluginHost;

/// <summary>
/// Status of a plugin load operation.
/// </summary>
public enum PluginLoadStatus
{
    /// <summary>Plugin loaded successfully.</summary>
    Success,
    /// <summary>Plugin is disabled in configuration.</summary>
    Disabled,
    /// <summary>Plugin file was not found.</summary>
    FileNotFound,
    /// <summary>Plugin failed to load due to an error.</summary>
    LoadError,
    /// <summary>Plugin API version does not match host version.</summary>
    VersionMismatch,
    /// <summary>Plugin has no IRuleProvider implementations.</summary>
    NoProviders,
    /// <summary>Plugin path was rejected by security validation.</summary>
    PathRejected
}

/// <summary>
/// Diagnostic information about a plugin load operation.
/// </summary>
/// <param name="Status">The load status.</param>
/// <param name="Message">A human-readable message describing the load result.</param>
/// <param name="ActualApiVersion">The plugin's declared API version (for version mismatches).</param>
/// <param name="ExpectedApiVersion">The host's expected API version (for version mismatches).</param>
/// <param name="ExceptionType">The type of exception that occurred (for load errors).</param>
/// <param name="StackTrace">The stack trace of the exception (for load errors).</param>
/// <param name="NativeDllProbeAttempts">List of native DLL probe attempts (for debugging native dependency issues).</param>
/// <param name="MissingNativeDll">The name of the missing native DLL (for DllNotFoundException).</param>
public sealed record PluginLoadDiagnostic(
    PluginLoadStatus Status,
    string? Message = null,
    int? ActualApiVersion = null,
    int? ExpectedApiVersion = null,
    string? ExceptionType = null,
    string? StackTrace = null,
    IReadOnlyList<string>? NativeDllProbeAttempts = null,
    string? MissingNativeDll = null
);

/// <summary>
/// Represents a loaded plugin with its rule providers and load diagnostic information.
/// </summary>
public sealed class LoadedPlugin : IDisposable
{
    private AssemblyLoadContext? _loadContext;
    private bool _disposed;

    public LoadedPlugin(
        string path,
        bool enabled,
        IReadOnlyList<IRuleProvider> providers,
        PluginLoadDiagnostic diagnostic,
        AssemblyLoadContext? loadContext = null)
    {
        Path = path;
        Enabled = enabled;
        Providers = providers;
        Diagnostic = diagnostic;
        _loadContext = loadContext;
    }

    public string Path { get; }
    public bool Enabled { get; }
    public IReadOnlyList<IRuleProvider> Providers { get; }
    public PluginLoadDiagnostic Diagnostic { get; }

    // Legacy error property for backwards compatibility
    public string? Error => Diagnostic.Status == PluginLoadStatus.Success ? null : Diagnostic.Message;

    /// <summary>
    /// Unloads the plugin's AssemblyLoadContext, releasing loaded assemblies.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _loadContext?.Unload();
            _loadContext = null;
            _disposed = true;
        }
    }
}

/// <summary>
/// Summary statistics for a batch of plugin load operations.
/// </summary>
/// <param name="TotalPlugins">Total number of plugins processed.</param>
/// <param name="SuccessCount">Number of plugins that loaded successfully.</param>
/// <param name="DisabledCount">Number of plugins that were disabled.</param>
/// <param name="ErrorCount">Number of plugins that failed to load.</param>
/// <param name="StatusBreakdown">Breakdown of plugin counts by status.</param>
public sealed record PluginLoadSummary(
    int TotalPlugins,
    int SuccessCount,
    int DisabledCount,
    int ErrorCount,
    IReadOnlyDictionary<PluginLoadStatus, int> StatusBreakdown
)
{
    /// <summary>
    /// Creates a summary from a collection of loaded plugins.
    /// </summary>
    public static PluginLoadSummary Create(IEnumerable<LoadedPlugin> plugins)
    {
        ArgumentNullException.ThrowIfNull(plugins);

        var total = 0;
        var successCount = 0;
        var disabledCount = 0;
        var errorCount = 0;
        var breakdown = new Dictionary<PluginLoadStatus, int>();

        foreach (var plugin in plugins)
        {
            total++;

            var status = plugin.Diagnostic.Status;
            if (!breakdown.TryAdd(status, 1))
            {
                breakdown[status]++;
            }

            switch (status)
            {
                case PluginLoadStatus.Success:
                    successCount++;
                    break;
                case PluginLoadStatus.Disabled:
                    disabledCount++;
                    break;
                default:
                    errorCount++;
                    break;
            }
        }

        return new PluginLoadSummary(total, successCount, disabledCount, errorCount, breakdown);
    }
};

/// <summary>
/// Loads plugin assemblies and discovers rule providers within them.
/// </summary>
public sealed class PluginLoader
{
    /// <summary>
    /// Validates that a plugin path is safe to load.
    /// Rejects UNC paths, absolute paths, and paths that escape the base directory.
    /// </summary>
    /// <param name="pluginPath">The plugin path from configuration.</param>
    /// <param name="baseDirectory">The base directory for resolving relative paths.</param>
    /// <returns>Null if valid; an error message if rejected.</returns>
    public static string? ValidatePluginPath(string pluginPath, string baseDirectory)
    {
        ArgumentNullException.ThrowIfNull(pluginPath);
        ArgumentNullException.ThrowIfNull(baseDirectory);

        if (pluginPath.StartsWith(@"\\", StringComparison.Ordinal) ||
            pluginPath.StartsWith("//", StringComparison.Ordinal))
        {
            return $"UNC paths are not allowed for plugins: {pluginPath}";
        }

        if (Path.IsPathRooted(pluginPath))
        {
            return $"Absolute paths are not allowed for plugins: {pluginPath}";
        }

        var fullPath = Path.GetFullPath(Path.Combine(baseDirectory, pluginPath));
        var fullBaseDir = Path.GetFullPath(baseDirectory);
        if (!fullBaseDir.EndsWith(Path.DirectorySeparatorChar))
        {
            fullBaseDir += Path.DirectorySeparatorChar;
        }

        if (!fullPath.StartsWith(fullBaseDir, StringComparison.OrdinalIgnoreCase))
        {
            return $"Plugin path escapes the project directory: {pluginPath}";
        }

        return null;
    }

    /// <summary>
    /// Loads plugins and returns both the loaded plugins and a summary of the load operation.
    /// </summary>
    public static (IReadOnlyList<LoadedPlugin> Plugins, PluginLoadSummary Summary) LoadWithSummary(
        IEnumerable<PluginDescriptor> plugins,
        string? baseDirectory = null)
    {
        var loadedPlugins = Load(plugins, baseDirectory);
        var summary = PluginLoadSummary.Create(loadedPlugins);
        return (loadedPlugins, summary);
    }

    /// <summary>
    /// Loads plugins from the specified descriptors and returns a list of loaded plugins.
    /// </summary>
    /// <param name="plugins">Plugin descriptors to load.</param>
    /// <param name="baseDirectory">Base directory for plugin path validation. When provided, paths are validated before loading.</param>
    public static IReadOnlyList<LoadedPlugin> Load(IEnumerable<PluginDescriptor> plugins, string? baseDirectory = null)
    {
        var results = new List<LoadedPlugin>();

        ArgumentNullException.ThrowIfNull(plugins);

        foreach (var plugin in plugins)
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

            if (baseDirectory is not null)
            {
                var validationError = ValidatePluginPath(plugin.Path, baseDirectory);
                if (validationError is not null)
                {
                    results.Add(new LoadedPlugin(
                        plugin.Path,
                        true,
                        Array.Empty<IRuleProvider>(),
                        new PluginLoadDiagnostic(
                            PluginLoadStatus.PathRejected,
                            validationError)));
                    continue;
                }
            }

            PluginLoadContext? loadContext = null;

            try
            {
                var fullPath = baseDirectory is not null
                    ? Path.GetFullPath(Path.Combine(baseDirectory, plugin.Path))
                    : Path.GetFullPath(plugin.Path);
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

                loadContext = new PluginLoadContext(fullPath);
                var assembly = loadContext.LoadFromAssemblyPath(fullPath);

                var (compatibleProviders, incompatibleProviders) = DiscoverProviders(assembly);

                // Check for version mismatches
                if (compatibleProviders.Count == 0 && incompatibleProviders.Count > 0)
                {
                    var mismatchedVersion = incompatibleProviders[0].PluginApiVersion;
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
            catch (DllNotFoundException dllEx)
            {
                var missingDll = ExtractMissingDllName(dllEx.Message);
                var probeAttempts = loadContext?.NativeDllProbeAttempts ?? Array.Empty<string>();

                results.Add(new LoadedPlugin(
                    plugin.Path,
                    true,
                    Array.Empty<IRuleProvider>(),
                    new PluginLoadDiagnostic(
                        PluginLoadStatus.LoadError,
                        $"Missing native dependency: {missingDll ?? "unknown"}",
                        ExceptionType: nameof(DllNotFoundException),
                        StackTrace: dllEx.StackTrace,
                        NativeDllProbeAttempts: probeAttempts,
                        MissingNativeDll: missingDll)));
            }
            catch (BadImageFormatException badImageEx)
            {
                results.Add(new LoadedPlugin(
                    plugin.Path,
                    true,
                    Array.Empty<IRuleProvider>(),
                    new PluginLoadDiagnostic(
                        PluginLoadStatus.LoadError,
                        "Invalid assembly format (check architecture: x64/x86/arm64)",
                        ExceptionType: nameof(BadImageFormatException),
                        StackTrace: badImageEx.StackTrace)));
            }
            catch (FileLoadException fileLoadEx)
            {
                results.Add(new LoadedPlugin(
                    plugin.Path,
                    true,
                    Array.Empty<IRuleProvider>(),
                    new PluginLoadDiagnostic(
                        PluginLoadStatus.LoadError,
                        $"Assembly load conflict: {fileLoadEx.Message}",
                        ExceptionType: nameof(FileLoadException),
                        StackTrace: fileLoadEx.StackTrace)));
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
        // Check assembly-level PluginApiVersion attribute first (no instantiation needed).
        // This prevents constructor side effects from running for incompatible plugins.
        var assemblyVersionAttr = assembly.GetCustomAttribute<PluginApiVersionAttribute>();
        if (assemblyVersionAttr is not null && assemblyVersionAttr.Version != PluginApi.CurrentVersion)
        {
            return (Array.Empty<IRuleProvider>(), [new IncompatibleProviderStub(assemblyVersionAttr.Version)]);
        }

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

    // Regex to extract DLL names from exception messages
    // Matches content inside single or double quotes (e.g., 'native_lib.dll' or "native_lib.dll")
    private static readonly Regex DllNamePattern = new(
        @"['""]([^'""]+)['""]",
        RegexOptions.Compiled);

    private static string? ExtractMissingDllName(string exceptionMessage)
    {
        // Common patterns in DllNotFoundException messages:
        // "Unable to load DLL 'native_lib.dll': ..."
        // "Unable to load DLL 'native_lib' or one of its dependencies. ..."
        // "The specified module could not be found. (Exception from HRESULT: 0x8007007E)"

        if (string.IsNullOrEmpty(exceptionMessage))
        {
            return null;
        }

        var match = DllNamePattern.Match(exceptionMessage);
        return match.Success ? match.Groups[1].Value : null;
    }

    /// <summary>
    /// Stub used to carry version information from an assembly-level attribute check
    /// without instantiating any actual provider types.
    /// </summary>
    private sealed class IncompatibleProviderStub(int version) : IRuleProvider
    {
        public string Name => "<incompatible>";
        public int PluginApiVersion => version;
        public IReadOnlyList<IRule> GetRules() => [];
    }
}
