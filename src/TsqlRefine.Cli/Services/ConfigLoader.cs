using System.Collections.Frozen;
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
    private const string ConfigDirName = ".tsqlrefine";
    internal const string DefaultPresetName = "recommended";

    /// <summary>
    /// Returns the ordered list of candidate paths for a settings file.
    /// Pure function for testability.
    /// </summary>
    internal static IReadOnlyList<string> GetCandidatePaths(
        string? explicitPath, string fileName, string cwd, string? homePath)
    {
        if (explicitPath is not null)
        {
            return [explicitPath];
        }

        var candidates = new List<string>(2)
        {
            Path.Combine(cwd, ConfigDirName, fileName)
        };

        if (!string.IsNullOrEmpty(homePath))
        {
            candidates.Add(Path.Combine(homePath, ConfigDirName, fileName));
        }

        return candidates;
    }

    /// <summary>
    /// Returns a warning message if a legacy config/ignore file exists in CWD root
    /// but was not loaded (because it is no longer a search candidate).
    /// </summary>
    internal static string? CheckLegacyFileWarning(string fileName, string? loadedPath, string cliFlag)
    {
        var cwd = Directory.GetCurrentDirectory();
        var legacyPath = Path.Combine(cwd, fileName);

        if (!File.Exists(legacyPath))
        {
            return null;
        }

        // If the loaded path IS the legacy path (via explicit --config/--ignorelist), no warning
        if (loadedPath is not null)
        {
            try
            {
                if (string.Equals(
                        Path.GetFullPath(legacyPath),
                        Path.GetFullPath(loadedPath),
                        StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }
            }
#pragma warning disable CA1031 // Guard against malformed explicit paths
            catch (Exception ex) when (
                ex is ArgumentException or NotSupportedException or PathTooLongException)
#pragma warning restore CA1031
            {
                // Invalid loadedPath — cannot compare, proceed to emit warning
            }
        }

        return $"Warning: Found {fileName} in current directory, but it is not loaded. " +
               $"Move it to .tsqlrefine/{fileName} or use {cliFlag} to specify it explicitly.";
    }

    private static string? ResolveSettingsFilePath(string? explicitPath, string fileName)
    {
        // Explicit path is returned as-is (existence check is done downstream)
        if (explicitPath is not null)
        {
            return explicitPath;
        }

        var cwd = Directory.GetCurrentDirectory();
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var candidates = GetCandidatePaths(null, fileName, cwd, home);

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string? ResolveConfigPath(CliArgs args)
    {
        return ResolveSettingsFilePath(args.ConfigPath, "tsqlrefine.json");
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

    public static Ruleset LoadRuleset(CliArgs args, TsqlRefineConfig config, IReadOnlyList<IRule> allRules)
    {
        // When --rule is specified, use a single-rule whitelist.
        // Rule ID validation is performed by ValidateRuleIdForFix.
        if (!string.IsNullOrWhiteSpace(args.RuleId))
        {
            return Ruleset.CreateSingleRuleWhitelist(args.RuleId);
        }

        var baseRuleset = ResolveBaseRuleset(args, config);

        // Enable plugin rules by default (bypass preset/ruleset whitelist)
        var pluginRuleIds = GetPluginRuleIds(allRules);
        var withPlugins = baseRuleset.WithPluginDefaults(pluginRuleIds);

        if (config.Rules is { Count: > 0 })
        {
            return withPlugins.WithOverrides(config.Rules);
        }

        return withPlugins;
    }

    private static Ruleset ResolveBaseRuleset(CliArgs args, TsqlRefineConfig config)
    {
        // CLI --preset always wins (already mutually exclusive with --ruleset via ValidateOptions)
        if (!string.IsNullOrWhiteSpace(args.Preset))
        {
            return LoadPresetRuleset(args.Preset);
        }

        // CLI --ruleset overrides any config-level preset or ruleset
        if (!string.IsNullOrWhiteSpace(args.RulesetPath))
        {
            return ResolveRulesetRef(args.RulesetPath);
        }

        // Config preset takes precedence over config ruleset
        if (!string.IsNullOrWhiteSpace(config.Preset))
        {
            return LoadPresetRuleset(config.Preset);
        }

        // Config ruleset
        if (!string.IsNullOrWhiteSpace(config.Ruleset))
        {
            return ResolveRulesetRef(config.Ruleset);
        }

        // Default: apply recommended preset
        return LoadPresetRuleset(DefaultPresetName);
    }

    private static Ruleset ResolveRulesetRef(string rulesetRef)
    {
        if (IsRulesetNameReference(rulesetRef))
        {
            return LoadNamedRuleset(rulesetRef);
        }

        return LoadRulesetFromPath(rulesetRef);
    }

    /// <summary>
    /// Returns true if the ruleset string is a name reference (not a file path).
    /// Name references contain no directory separators and do not end with .json.
    /// </summary>
    internal static bool IsRulesetNameReference(string ruleset)
        => !ruleset.Contains('/') && !ruleset.Contains('\\')
           && !ruleset.EndsWith(".json", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Resolves a named ruleset to a file path by searching standard directories.
    /// Returns null if the named ruleset is not found.
    /// </summary>
    internal static string? ResolveNamedRulesetPath(
        string name, string? cwd = null, string? homePath = null)
    {
        cwd ??= Directory.GetCurrentDirectory();
        homePath ??= Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        // 1. CWD/.tsqlrefine/rulesets/{name}.json
        var candidate = Path.Combine(cwd, ConfigDirName, "rulesets", $"{name}.json");
        if (File.Exists(candidate))
        {
            return Path.GetFullPath(candidate);
        }

        // 2. HOME/.tsqlrefine/rulesets/{name}.json
        if (!string.IsNullOrEmpty(homePath))
        {
            candidate = Path.Combine(homePath, ConfigDirName, "rulesets", $"{name}.json");
            if (File.Exists(candidate))
            {
                return Path.GetFullPath(candidate);
            }
        }

        return null;
    }

    private static Ruleset LoadNamedRuleset(string name)
    {
        var path = ResolveNamedRulesetPath(name);
        if (path is null)
        {
            throw new ConfigException(
                $"Named ruleset '{name}' not found. " +
                $"Place a ruleset file at .tsqlrefine/rulesets/{name}.json");
        }

        return LoadRulesetFromPath(path);
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
        var rules = new List<IRule>();
        rules.AddRange(new BuiltinRuleProvider().GetRules());

        var pluginConfigs = config.Plugins ?? Array.Empty<PluginConfig>();

        if (pluginConfigs.Count > 0 && !args.AllowPlugins)
        {
            stderr?.WriteLine(
                $"Warning: {pluginConfigs.Count} plugin(s) configured but not loaded. Use --allow-plugins to enable plugin loading.");
            return rules;
        }

        if (pluginConfigs.Count == 0)
        {
            return rules;
        }

        var configPath = GetConfigPath(args);
        var baseDirectory = configPath is not null
            ? Path.GetDirectoryName(Path.GetFullPath(configPath))!
            : Directory.GetCurrentDirectory();

        var plugins = ResolvePluginDescriptors(pluginConfigs, baseDirectory);

        var loaded = PluginLoader.Load(plugins, baseDirectory);

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
        var path = ResolveSettingsFilePath(ignoreListPath, "tsqlrefine.ignore");

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

    private static readonly Lazy<FrozenSet<string>> BuiltinRuleIds = new(() =>
        new BuiltinRuleProvider().GetRules()
            .Select(r => r.Metadata.RuleId)
            .ToFrozenSet(StringComparer.OrdinalIgnoreCase));

    private static IEnumerable<string> GetPluginRuleIds(IReadOnlyList<IRule> allRules)
    {
        var builtinIds = BuiltinRuleIds.Value;
        foreach (var rule in allRules)
        {
            if (!builtinIds.Contains(rule.Metadata.RuleId))
            {
                yield return rule.Metadata.RuleId;
            }
        }
    }

    /// <summary>
    /// Resolves plugin descriptors, searching known directories for filename-only paths.
    /// </summary>
    internal static IReadOnlyList<PluginDescriptor> ResolvePluginDescriptors(
        IReadOnlyList<PluginConfig> pluginConfigs,
        string baseDirectory,
        string? cwd = null,
        string? homePath = null)
    {
        cwd ??= Directory.GetCurrentDirectory();
        homePath ??= Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        var descriptors = new List<PluginDescriptor>(pluginConfigs.Count);

        foreach (var config in pluginConfigs)
        {
            if (IsFilenameOnly(config.Path))
            {
                var resolvedPath = SearchPluginPath(config.Path, baseDirectory, cwd, homePath);
                descriptors.Add(new PluginDescriptor(config.Path, config.Enabled, resolvedPath));
            }
            else
            {
                descriptors.Add(new PluginDescriptor(config.Path, config.Enabled));
            }
        }

        return descriptors;
    }

    private static bool IsFilenameOnly(string path)
        => Path.GetFileName(path) == path;

    /// <summary>
    /// Searches for a plugin filename in the standard search directories.
    /// Returns the full resolved path if found, or null if not found in any location.
    /// </summary>
    internal static string? SearchPluginPath(
        string filename, string baseDirectory, string cwd, string? homePath)
    {
        // 1. baseDirectory (config file directory or CWD)
        var candidate = Path.Combine(baseDirectory, filename);
        if (File.Exists(candidate))
        {
            return Path.GetFullPath(candidate);
        }

        // 2. CWD/.tsqlrefine/plugins/
        candidate = Path.Combine(cwd, ConfigDirName, "plugins", filename);
        if (File.Exists(candidate))
        {
            return Path.GetFullPath(candidate);
        }

        // 3. HOME/.tsqlrefine/plugins/
        if (!string.IsNullOrEmpty(homePath))
        {
            candidate = Path.Combine(homePath, ConfigDirName, "plugins", filename);
            if (File.Exists(candidate))
            {
                return Path.GetFullPath(candidate);
            }
        }

        return null;
    }
}
