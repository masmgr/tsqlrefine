using System.Text;
using TsqlRefine.Core;
using TsqlRefine.Core.Config;
using TsqlRefine.PluginHost;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules;

namespace TsqlRefine.Cli.Services;

public sealed class ConfigLoader
{
    public TsqlRefineConfig LoadConfig(CliArgs args)
    {
        var path = args.ConfigPath;
        if (path is null)
        {
            var defaultPath = Path.Combine(Directory.GetCurrentDirectory(), "tsqlrefine.json");
            if (File.Exists(defaultPath))
            {
                path = defaultPath;
            }
        }

        if (path is null)
        {
            return TsqlRefineConfig.Default;
        }

        if (!File.Exists(path))
        {
            throw new ConfigException($"Config file not found: {path}");
        }

        try
        {
            return TsqlRefineConfig.Load(path);
        }
        catch (Exception ex)
        {
            throw new ConfigException($"Failed to parse config: {ex.Message}");
        }
    }

    public Ruleset? LoadRuleset(CliArgs args, TsqlRefineConfig config)
    {
        var path = args.RulesetPath ?? config.Ruleset;

        if (!string.IsNullOrWhiteSpace(args.Preset))
        {
            path = Path.Combine(Directory.GetCurrentDirectory(), "rulesets", $"{args.Preset}.json");
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        if (!File.Exists(path))
        {
            throw new ConfigException($"Ruleset file not found: {path}");
        }

        try
        {
            return Ruleset.Load(path);
        }
        catch (Exception ex)
        {
            throw new ConfigException($"Failed to parse ruleset: {ex.Message}");
        }
    }

    public IReadOnlyList<IRule> LoadRules(CliArgs args, TsqlRefineConfig config, TextWriter? stderr = null)
    {
        _ = args;

        var rules = new List<IRule>();
        rules.AddRange(new BuiltinRuleProvider().GetRules());

        var plugins = (config.Plugins ?? Array.Empty<PluginConfig>())
            .Select(p => new PluginDescriptor(p.Path, p.Enabled))
            .ToArray();

        var loaded = PluginLoader.Load(plugins);

        // Report plugin loading issues with summary
        if (stderr is not null)
        {
            var failedPlugins = loaded.Where(p => p.Diagnostic.Status != PluginLoadStatus.Success && p.Diagnostic.Status != PluginLoadStatus.Disabled).ToList();
            if (failedPlugins.Count > 0)
            {
                var totalPlugins = loaded.Count(p => p.Enabled);
                stderr.WriteLine($"Warning: {failedPlugins.Count} of {totalPlugins} plugin{(totalPlugins == 1 ? "" : "s")} failed to load. Run 'tsqlrefine list-plugins' for details.");

                foreach (var p in failedPlugins)
                {
                    stderr.Write($"  {p.Path}: ");

                    if (p.Diagnostic.Status == PluginLoadStatus.VersionMismatch)
                    {
                        stderr.WriteLine($"API version mismatch (plugin uses v{p.Diagnostic.ActualApiVersion}, host expects v{p.Diagnostic.ExpectedApiVersion})");
                        stderr.WriteLine($"    Hint: Rebuild plugin against TsqlRefine.PluginSdk v{p.Diagnostic.ExpectedApiVersion}.x.x");
                    }
                    else if (p.Diagnostic.Status == PluginLoadStatus.LoadError)
                    {
                        stderr.WriteLine($"{p.Diagnostic.ExceptionType}: {p.Diagnostic.Message}");
                        if (p.Diagnostic.MissingNativeDll is not null)
                        {
                            stderr.WriteLine($"    Hint: Install native library '{p.Diagnostic.MissingNativeDll}'");
                        }
                        else if (p.Diagnostic.ExceptionType == "BadImageFormatException")
                        {
                            stderr.WriteLine($"    Hint: Check plugin architecture matches host (x64/x86/arm64)");
                        }
                    }
                    else if (p.Diagnostic.Status == PluginLoadStatus.FileNotFound)
                    {
                        stderr.WriteLine($"File not found");
                    }
                    else if (p.Diagnostic.Status == PluginLoadStatus.NoProviders)
                    {
                        stderr.WriteLine($"No rule providers found");
                    }
                    else
                    {
                        stderr.WriteLine(p.Diagnostic.Message ?? "Unknown error");
                    }
                }

                stderr.WriteLine();
            }
        }

        foreach (var p in loaded)
        {
            foreach (var provider in p.Providers)
            {
                rules.AddRange(provider.GetRules());
            }
        }

        return rules;
    }

    public List<string> LoadIgnorePatterns(string? ignoreListPath)
    {
        // Check explicit path first, then default tsqlrefine.ignore
        var path = ignoreListPath;
        if (path is null)
        {
            var defaultPath = Path.Combine(Directory.GetCurrentDirectory(), "tsqlrefine.ignore");
            if (File.Exists(defaultPath))
                path = defaultPath;
        }

        if (path is null)
            return new List<string>();

        if (!File.Exists(path))
            throw new ConfigException($"Ignore list file not found: {path}");

        try
        {
            var lines = File.ReadAllLines(path, Encoding.UTF8);
            return lines
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrEmpty(l) && !l.StartsWith('#'))
                .ToList();
        }
        catch (Exception ex) when (ex is not ConfigException)
        {
            throw new ConfigException($"Failed to read ignore list: {ex.Message}");
        }
    }
}
