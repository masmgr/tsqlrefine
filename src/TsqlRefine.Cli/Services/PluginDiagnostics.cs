using TsqlRefine.PluginHost;

namespace TsqlRefine.Cli.Services;

public sealed class PluginDiagnostics
{
    public string GetRemediationHint(PluginLoadDiagnostic diagnostic)
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

    public async Task WritePluginSummaryAsync(
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

    private async Task WritePluginDetailsAsync(LoadedPlugin plugin, bool verbose, TextWriter stdout)
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

    private async Task WriteSuccessDetailsAsync(LoadedPlugin plugin, TextWriter stdout)
    {
        await stdout.WriteLineAsync($"  Providers: {plugin.Providers.Count}");
        var ruleCount = plugin.Providers.SelectMany(prov => prov.GetRules()).Count();
        await stdout.WriteLineAsync($"  Rules: {ruleCount}");
    }

    private async Task WriteVersionMismatchDetailsAsync(LoadedPlugin plugin, TextWriter stdout)
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

    private async Task WriteLoadErrorDetailsAsync(LoadedPlugin plugin, bool verbose, TextWriter stdout)
    {
        await stdout.WriteLineAsync($"  Error: {plugin.Diagnostic.ExceptionType}: {plugin.Diagnostic.Message}");

        // Show native DLL probe attempts if available
        if (plugin.Diagnostic.MissingNativeDll is not null && plugin.Diagnostic.NativeDllProbeAttempts is not null)
        {
            await stdout.WriteLineAsync($"  Missing Native DLL: {plugin.Diagnostic.MissingNativeDll}");
            await stdout.WriteLineAsync($"  Probe Attempts ({plugin.Diagnostic.NativeDllProbeAttempts.Count}):");
            var attempts = plugin.Diagnostic.NativeDllProbeAttempts.Take(verbose ? int.MaxValue : 5);
            foreach (var attempt in attempts)
            {
                await stdout.WriteLineAsync($"    {attempt}");
            }
            if (!verbose && plugin.Diagnostic.NativeDllProbeAttempts.Count > 5)
            {
                await stdout.WriteLineAsync($"    ... ({plugin.Diagnostic.NativeDllProbeAttempts.Count - 5} more, use --verbose to see all)");
            }
        }

        // Show stack trace (simplified unless --verbose)
        if (!string.IsNullOrWhiteSpace(plugin.Diagnostic.StackTrace))
        {
            await WriteStackTraceAsync(plugin.Diagnostic.StackTrace, verbose, stdout);
        }

        var hint = GetRemediationHint(plugin.Diagnostic);
        if (!string.IsNullOrEmpty(hint))
        {
            await stdout.WriteLineAsync($"  Hint: {hint}");
        }
    }

    private async Task WriteStackTraceAsync(string stackTrace, bool verbose, TextWriter stdout)
    {
        if (verbose)
        {
            await stdout.WriteLineAsync($"  Stack Trace:");
            foreach (var line in stackTrace.Split('\n'))
            {
                await stdout.WriteLineAsync($"    {line.TrimEnd()}");
            }
        }
        else
        {
            var lines = stackTrace.Split('\n').Where(l => !string.IsNullOrWhiteSpace(l)).Take(3).ToList();
            if (lines.Count > 0)
            {
                await stdout.WriteLineAsync($"  Stack Trace (top 3 frames, use --verbose for full trace):");
                foreach (var line in lines)
                {
                    await stdout.WriteLineAsync($"    {line.TrimEnd()}");
                }
            }
        }
    }

    private async Task WriteGenericErrorDetailsAsync(LoadedPlugin plugin, TextWriter stdout)
    {
        await stdout.WriteLineAsync($"  Message: {plugin.Diagnostic.Message}");

        var hint = GetRemediationHint(plugin.Diagnostic);
        if (!string.IsNullOrEmpty(hint))
        {
            await stdout.WriteLineAsync($"  Hint: {hint}");
        }
    }

    private PluginLoadSummary CalculateSummary(IReadOnlyList<LoadedPlugin> plugins)
    {
        var totalPlugins = plugins.Count;
        var successCount = plugins.Count(p => p.Diagnostic.Status == PluginLoadStatus.Success);
        var disabledCount = plugins.Count(p => p.Diagnostic.Status == PluginLoadStatus.Disabled);
        var errorCount = plugins.Count(p => p.Diagnostic.Status != PluginLoadStatus.Success && p.Diagnostic.Status != PluginLoadStatus.Disabled);

        return new PluginLoadSummary(totalPlugins, successCount, disabledCount, errorCount);
    }

    private sealed record PluginLoadSummary(int TotalPlugins, int SuccessCount, int DisabledCount, int ErrorCount);
}
