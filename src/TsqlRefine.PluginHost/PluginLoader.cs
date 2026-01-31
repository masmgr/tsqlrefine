using System.Reflection;
using System.Runtime.Loader;
using System.Text.RegularExpressions;
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
    string? StackTrace = null,
    IReadOnlyList<string>? NativeDllProbeAttempts = null,
    string? MissingNativeDll = null
);

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

public sealed record PluginLoadSummary(
    int TotalPlugins,
    int SuccessCount,
    int DisabledCount,
    int ErrorCount,
    IReadOnlyDictionary<PluginLoadStatus, int> StatusBreakdown
)
{
    public static PluginLoadSummary Create(IEnumerable<LoadedPlugin> plugins)
    {
        var pluginList = plugins.ToList();
        var total = pluginList.Count;
        var successCount = pluginList.Count(p => p.Diagnostic.Status == PluginLoadStatus.Success);
        var disabledCount = pluginList.Count(p => p.Diagnostic.Status == PluginLoadStatus.Disabled);
        var errorCount = pluginList.Count(p =>
            p.Diagnostic.Status != PluginLoadStatus.Success &&
            p.Diagnostic.Status != PluginLoadStatus.Disabled);

        var breakdown = pluginList
            .GroupBy(p => p.Diagnostic.Status)
            .ToDictionary(g => g.Key, g => g.Count());

        return new PluginLoadSummary(total, successCount, disabledCount, errorCount, breakdown);
    }
};

public sealed class PluginLoader
{
    public static (IReadOnlyList<LoadedPlugin> Plugins, PluginLoadSummary Summary) LoadWithSummary(IEnumerable<PluginDescriptor> plugins)
    {
        var loadedPlugins = Load(plugins);
        var summary = PluginLoadSummary.Create(loadedPlugins);
        return (loadedPlugins, summary);
    }

    public static IReadOnlyList<LoadedPlugin> Load(IEnumerable<PluginDescriptor> plugins)
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

            PluginLoadContext? loadContext = null;

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

                loadContext = new PluginLoadContext(fullPath);
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
}
