using TsqlRefine.PluginHost;

namespace TsqlRefine.Cli.Services;

/// <summary>
/// Provides diagnostic messages and remediation hints for plugin loading issues.
/// </summary>
public sealed class PluginDiagnostics
{
    public static string GetRemediationHint(PluginLoadDiagnostic diagnostic)
    {
        return diagnostic.Status switch
        {
            PluginLoadStatus.FileNotFound =>
                "Check the plugin path in your tsqlrefine.json configuration.",

            PluginLoadStatus.VersionMismatch =>
                $"Rebuild the plugin targeting PluginSdk API version {diagnostic.ExpectedApiVersion}.",

            PluginLoadStatus.LoadError when diagnostic.MissingNativeDll is not null =>
                $"Install or provide the native library '{diagnostic.MissingNativeDll}' in the plugin directory or runtimes/ folder.",

            PluginLoadStatus.LoadError when diagnostic.ExceptionType == "BadImageFormatException" =>
                "Ensure the plugin DLL matches the host architecture (x64/x86/arm64).",

            PluginLoadStatus.NoProviders =>
                "Ensure the plugin assembly contains a public class implementing IRuleProvider.",

            _ => string.Empty
        };
    }

    /// <summary>
    /// Writes a brief summary of plugin loading failures to stderr.
    /// Used during lint/format/fix commands to warn about failed plugins.
    /// </summary>
    public static void WriteFailedPluginWarnings(IReadOnlyList<LoadedPlugin> plugins, TextWriter stderr)
    {
        List<LoadedPlugin>? failedPlugins = null;
        var totalEnabled = 0;
        foreach (var plugin in plugins)
        {
            if (plugin.Enabled)
            {
                totalEnabled++;
            }

            var status = plugin.Diagnostic.Status;
            if (status != PluginLoadStatus.Success && status != PluginLoadStatus.Disabled)
            {
                failedPlugins ??= new List<LoadedPlugin>();
                failedPlugins.Add(plugin);
            }
        }

        if (failedPlugins is null || failedPlugins.Count == 0)
            return;

        stderr.WriteLine($"Warning: {failedPlugins.Count} of {totalEnabled} plugin{(totalEnabled == 1 ? "" : "s")} failed to load. Run 'tsqlrefine list-plugins' for details.");

        foreach (var p in failedPlugins)
        {
            stderr.Write($"  {p.Path}: ");
            WritePluginErrorSummary(p, stderr);
        }

        stderr.WriteLine();
    }

    private static void WritePluginErrorSummary(LoadedPlugin plugin, TextWriter stderr)
    {
        switch (plugin.Diagnostic.Status)
        {
            case PluginLoadStatus.VersionMismatch:
                stderr.WriteLine($"API version mismatch (plugin uses v{plugin.Diagnostic.ActualApiVersion}, host expects v{plugin.Diagnostic.ExpectedApiVersion})");
                stderr.WriteLine($"    Hint: Rebuild plugin against TsqlRefine.PluginSdk v{plugin.Diagnostic.ExpectedApiVersion}.x.x");
                break;

            case PluginLoadStatus.LoadError:
                stderr.WriteLine($"{plugin.Diagnostic.ExceptionType}: {plugin.Diagnostic.Message}");
                if (plugin.Diagnostic.MissingNativeDll is not null)
                {
                    stderr.WriteLine($"    Hint: Install native library '{plugin.Diagnostic.MissingNativeDll}'");
                }
                else if (plugin.Diagnostic.ExceptionType == "BadImageFormatException")
                {
                    stderr.WriteLine($"    Hint: Check plugin architecture matches host (x64/x86/arm64)");
                }
                break;

            case PluginLoadStatus.FileNotFound:
                stderr.WriteLine("File not found");
                break;

            case PluginLoadStatus.NoProviders:
                stderr.WriteLine("No rule providers found");
                break;

            default:
                stderr.WriteLine(plugin.Diagnostic.Message ?? "Unknown error");
                break;
        }
    }

    public static async Task WritePluginSummaryAsync(
        IReadOnlyList<LoadedPlugin> plugins,
        bool verbose,
        TextWriter stdout)
    {
        var summary = CalculateSummary(plugins);

        // Display summary header
        await stdout.WriteLineAsync("Plugin Load Summary:");
        await stdout.WriteLineAsync($"  Total: {summary.TotalPlugins} plugin{(summary.TotalPlugins == 1 ? "" : "s")}");
        if (summary.TotalPlugins > 0)
        {
            await stdout.WriteLineAsync($"  Loaded: {summary.SuccessCount} ({summary.SuccessCount * 100 / summary.TotalPlugins}%)");
            await stdout.WriteLineAsync($"  Disabled: {summary.DisabledCount} ({summary.DisabledCount * 100 / summary.TotalPlugins}%)");
            await stdout.WriteLineAsync($"  Failed: {summary.ErrorCount} ({summary.ErrorCount * 100 / summary.TotalPlugins}%)");
        }
        await stdout.WriteLineAsync();

        // Group plugins by status
        var grouped = plugins
            .OrderBy(p => p.Diagnostic.Status switch
            {
                PluginLoadStatus.Success => 0,
                PluginLoadStatus.VersionMismatch => 1,
                PluginLoadStatus.LoadError => 2,
                PluginLoadStatus.FileNotFound => 3,
                PluginLoadStatus.NoProviders => 4,
                PluginLoadStatus.Disabled => 5,
                _ => 6
            })
            .ToList();

        foreach (var p in grouped)
        {
            await WritePluginDetailsAsync(p, verbose, stdout);
        }
    }

    private static async Task WritePluginDetailsAsync(LoadedPlugin plugin, bool verbose, TextWriter stdout)
    {
        var statusIcon = plugin.Diagnostic.Status switch
        {
            PluginLoadStatus.Success => "✓",
            PluginLoadStatus.Disabled => "○",
            PluginLoadStatus.FileNotFound => "✗",
            PluginLoadStatus.LoadError => "✗",
            PluginLoadStatus.VersionMismatch => "⚠",
            PluginLoadStatus.NoProviders => "⚠",
            _ => "?"
        };

        await stdout.WriteLineAsync($"{statusIcon} {plugin.Path}");
        await stdout.WriteLineAsync($"  Status: {plugin.Diagnostic.Status}");

        if (!plugin.Enabled)
        {
            await stdout.WriteLineAsync($"  Message: {plugin.Diagnostic.Message}");
        }
        else if (plugin.Diagnostic.Status == PluginLoadStatus.Success)
        {
            await WriteSuccessDetailsAsync(plugin, stdout);
        }
        else if (plugin.Diagnostic.Status == PluginLoadStatus.VersionMismatch)
        {
            await WriteVersionMismatchDetailsAsync(plugin, stdout);
        }
        else if (plugin.Diagnostic.Status == PluginLoadStatus.LoadError)
        {
            await WriteLoadErrorDetailsAsync(plugin, verbose, stdout);
        }
        else
        {
            await WriteGenericErrorDetailsAsync(plugin, stdout);
        }

        await stdout.WriteLineAsync();
    }

    private static async Task WriteSuccessDetailsAsync(LoadedPlugin plugin, TextWriter stdout)
    {
        await stdout.WriteLineAsync($"  Providers: {plugin.Providers.Count}");
        var ruleCount = plugin.Providers.SelectMany(prov => prov.GetRules()).Count();
        await stdout.WriteLineAsync($"  Rules: {ruleCount}");
    }

    private static async Task WriteVersionMismatchDetailsAsync(LoadedPlugin plugin, TextWriter stdout)
    {
        await stdout.WriteLineAsync($"  Message: {plugin.Diagnostic.Message}");
        await stdout.WriteLineAsync($"  Expected API Version: {plugin.Diagnostic.ExpectedApiVersion}");
        await stdout.WriteLineAsync($"  Actual API Version: {plugin.Diagnostic.ActualApiVersion}");

        var hint = GetRemediationHint(plugin.Diagnostic);
        if (!string.IsNullOrEmpty(hint))
        {
            await stdout.WriteLineAsync($"  Hint: {hint}");
        }
    }

    private static async Task WriteLoadErrorDetailsAsync(LoadedPlugin plugin, bool verbose, TextWriter stdout)
    {
        await stdout.WriteLineAsync($"  Error: {plugin.Diagnostic.ExceptionType}: {plugin.Diagnostic.Message}");

        if (verbose)
        {
            // Show native DLL probe attempts only in verbose mode
            if (plugin.Diagnostic.MissingNativeDll is not null && plugin.Diagnostic.NativeDllProbeAttempts is not null)
            {
                await stdout.WriteLineAsync($"  Missing Native DLL: {plugin.Diagnostic.MissingNativeDll}");
                await stdout.WriteLineAsync($"  Probe Attempts ({plugin.Diagnostic.NativeDllProbeAttempts.Count}):");
                foreach (var attempt in plugin.Diagnostic.NativeDllProbeAttempts)
                {
                    await stdout.WriteLineAsync($"    {attempt}");
                }
            }

            // Show full stack trace only in verbose mode
            if (!string.IsNullOrWhiteSpace(plugin.Diagnostic.StackTrace))
            {
                await stdout.WriteLineAsync($"  Stack Trace:");
                foreach (var line in plugin.Diagnostic.StackTrace.Split('\n'))
                {
                    await stdout.WriteLineAsync($"    {line.TrimEnd()}");
                }
            }
        }
        else
        {
            var hasDetails = plugin.Diagnostic.NativeDllProbeAttempts is { Count: > 0 }
                || !string.IsNullOrWhiteSpace(plugin.Diagnostic.StackTrace);
            if (hasDetails)
            {
                await stdout.WriteLineAsync($"  (use --verbose for full details)");
            }
        }

        var hint = GetRemediationHint(plugin.Diagnostic);
        if (!string.IsNullOrEmpty(hint))
        {
            await stdout.WriteLineAsync($"  Hint: {hint}");
        }
    }

    private static async Task WriteGenericErrorDetailsAsync(LoadedPlugin plugin, TextWriter stdout)
    {
        await stdout.WriteLineAsync($"  Message: {plugin.Diagnostic.Message}");

        var hint = GetRemediationHint(plugin.Diagnostic);
        if (!string.IsNullOrEmpty(hint))
        {
            await stdout.WriteLineAsync($"  Hint: {hint}");
        }
    }

    private static PluginLoadSummary CalculateSummary(IReadOnlyList<LoadedPlugin> plugins)
    {
        var totalPlugins = plugins.Count;
        var successCount = 0;
        var disabledCount = 0;
        var errorCount = 0;

        foreach (var plugin in plugins)
        {
            switch (plugin.Diagnostic.Status)
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

        return new PluginLoadSummary(totalPlugins, successCount, disabledCount, errorCount);
    }

    private sealed record PluginLoadSummary(int TotalPlugins, int SuccessCount, int DisabledCount, int ErrorCount);
}
