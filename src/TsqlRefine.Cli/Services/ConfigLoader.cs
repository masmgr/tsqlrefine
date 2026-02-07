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
        // --rule が指定された場合、単一ルールのホワイトリストを作成
        // バリデーションは ValidateRuleId で行う
        if (!string.IsNullOrWhiteSpace(args.RuleId))
        {
            return Ruleset.CreateSingleRuleWhitelist(args.RuleId);
        }

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
    /// fix コマンドで --rule オプションが指定された場合、ルールIDのバリデーションを行う。
    /// 存在しないルールID、または Fixable でないルールの場合は ConfigException をスローする。
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
}
