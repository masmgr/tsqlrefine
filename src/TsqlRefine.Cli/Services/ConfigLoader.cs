using System.Text;
using TsqlRefine.Core.Config;
using TsqlRefine.PluginHost;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules;

namespace TsqlRefine.Cli.Services;

/// <summary>
/// Loads and merges configuration from files, CLI arguments, and presets.
/// </summary>
public sealed class ConfigLoader
{
    private static string? ResolveConfigPath(CliArgs args)
    {
        if (args.ConfigPath is not null)
        {
            return args.ConfigPath;
        }

        var defaultPath = Path.Combine(Directory.GetCurrentDirectory(), "tsqlrefine.json");
        return File.Exists(defaultPath) ? defaultPath : null;
    }

    /// <summary>
    /// Gets the path to the config file that would be loaded, or null if using defaults.
    /// </summary>
    public static string? GetConfigPath(CliArgs args)
    {
        var path = ResolveConfigPath(args);

        if (path is not null && File.Exists(path))
        {
            return Path.GetFullPath(path);
        }

        return null;
    }

    public static TsqlRefineConfig LoadConfig(CliArgs args)
    {
        var path = ResolveConfigPath(args);

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

#pragma warning disable CA1031 // Wrap config parse failures into ConfigException
        catch (Exception ex)
#pragma warning restore CA1031
        {
            throw new ConfigException($"Failed to parse config: {ex.Message}");
        }
    }

    public static Ruleset? LoadRuleset(CliArgs args, TsqlRefineConfig config)
    {
        // When --rule is specified, use a single-rule whitelist.
        // Rule ID validation is performed by ValidateRuleIdForFix.
        if (!string.IsNullOrWhiteSpace(args.RuleId))
        {
            return Ruleset.CreateSingleRuleWhitelist(args.RuleId);
        }

        var baseRuleset = ResolveBaseRuleset(args, config);

        if (config.Rules is { Count: > 0 })
        {
            return (baseRuleset ?? Ruleset.Empty).WithOverrides(config.Rules);
        }

        return baseRuleset;
    }

    private static Ruleset? ResolveBaseRuleset(CliArgs args, TsqlRefineConfig config)
    {
        // Preset resolution priority: CLI --preset > config preset
        var presetName = args.Preset ?? config.Preset;
        if (!string.IsNullOrWhiteSpace(presetName))
        {
            return LoadPresetRuleset(presetName);
        }

        // Custom ruleset path resolution: CLI --ruleset > config ruleset
        var rulesetPath = args.RulesetPath ?? config.Ruleset;
        return string.IsNullOrWhiteSpace(rulesetPath)
            ? null
            : LoadRulesetFromPath(rulesetPath);
    }

    private static Ruleset LoadPresetRuleset(string presetName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "rulesets", $"{presetName}.json");

        if (!File.Exists(path))
        {
            var available = DiscoverPresetNames();
            var list = available.Count > 0
                ? string.Join(", ", available)
                : "none found";
            throw new ConfigException(
                $"Unknown preset: '{presetName}'. Available presets: {list}");
        }

        return Ruleset.Load(path);
    }

    private static Ruleset LoadRulesetFromPath(string rulesetPath)
    {
        if (!File.Exists(rulesetPath))
        {
            throw new ConfigException($"Ruleset file not found: {rulesetPath}");
        }

        try
        {
            return Ruleset.Load(rulesetPath);
        }

#pragma warning disable CA1031 // Wrap ruleset parse failures into ConfigException
        catch (Exception ex)
#pragma warning restore CA1031
        {
            throw new ConfigException($"Failed to parse ruleset: {ex.Message}");
        }
    }

    public static IReadOnlyList<IRule> LoadRules(CliArgs args, TsqlRefineConfig config, TextWriter? stderr = null)
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
            PluginDiagnostics.WriteFailedPluginWarnings(loaded, stderr);
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

    /// <summary>
    /// Validates --rule for fix command.
    /// Throws ConfigException when the rule does not exist or is not fixable.
    /// </summary>
    public static void ValidateRuleIdForFix(CliArgs args, IReadOnlyList<IRule> rules)
    {
        if (string.IsNullOrWhiteSpace(args.RuleId))
        {
            return;
        }

        var ruleId = args.RuleId;
        var rule = rules.FirstOrDefault(r =>
            string.Equals(r.Metadata.RuleId, ruleId, StringComparison.OrdinalIgnoreCase));

        if (rule is null)
        {
            throw new ConfigException($"Unknown rule ID: {ruleId}");
        }

        if (!rule.Metadata.Fixable)
        {
            throw new ConfigException($"Rule '{ruleId}' does not support auto-fix.");
        }
    }

    public static List<string> LoadIgnorePatterns(string? ignoreListPath)
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
            var patterns = new List<string>();
            foreach (var line in File.ReadLines(path, Encoding.UTF8))
            {
                var trimmed = line.Trim();
                if (trimmed.Length == 0 || trimmed.StartsWith('#'))
                {
                    continue;
                }

                patterns.Add(trimmed);
            }

            return patterns;
        }

#pragma warning disable CA1031 // Wrap ignore list read failures into ConfigException
        catch (Exception ex) when (ex is not ConfigException)
#pragma warning restore CA1031
        {
            throw new ConfigException($"Failed to read ignore list: {ex.Message}");
        }
    }

    private static List<string> DiscoverPresetNames()
    {
        var rulesetsDir = Path.Combine(AppContext.BaseDirectory, "rulesets");
        if (!Directory.Exists(rulesetsDir))
        {
            return [];
        }

        return Directory.GetFiles(rulesetsDir, "*.json")
            .Select(f => Path.GetFileNameWithoutExtension(f))
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
