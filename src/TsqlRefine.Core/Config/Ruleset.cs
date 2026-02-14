using System.Text.Json;
using System.Text.Json.Serialization;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Core.Config;

/// <summary>
/// Severity level for per-rule configuration.
/// Combines enablement and severity into a single value.
/// </summary>
public enum RuleSeverityLevel
{
    /// <summary>Rule is disabled.</summary>
    None,
    /// <summary>Rule is enabled with its default severity.</summary>
    Inherit,
    /// <summary>Rule is enabled with Error severity.</summary>
    Error,
    /// <summary>Rule is enabled with Warning severity.</summary>
    Warning,
    /// <summary>Rule is enabled with Information severity.</summary>
    Info
}

/// <summary>
/// Represents a rule configuration within a ruleset.
/// </summary>
/// <param name="Id">The unique identifier of the rule.</param>
/// <param name="Severity">Severity level string: "error", "warning", "info", "inherit", or "none".</param>
public sealed record RulesetRule(string Id, string? Severity = null);

/// <summary>
/// Represents a collection of rule configurations that determine which rules are enabled during analysis.
/// When loaded from a ruleset file (whitelist mode), only listed rules are enabled.
/// </summary>
public sealed class Ruleset
{
    private readonly IReadOnlyList<RulesetRule> _rules;
    private readonly Dictionary<string, RuleSeverityLevel>? _ruleCache;
    private readonly string? _singleRuleWhitelist;
    private readonly bool _whitelistMode;

    [JsonConstructor]
    public Ruleset(IReadOnlyList<RulesetRule>? rules) : this(rules, null, whitelistMode: true)
    {
    }

    private Ruleset(IReadOnlyList<RulesetRule>? rules, string? singleRuleWhitelist, bool whitelistMode = true)
    {
        _rules = rules ?? Array.Empty<RulesetRule>();
        _singleRuleWhitelist = singleRuleWhitelist;
        _whitelistMode = whitelistMode;

        // Pre-build cache for O(1) lookup
        if (_rules.Count > 0)
        {
            _ruleCache = new Dictionary<string, RuleSeverityLevel>(_rules.Count, StringComparer.OrdinalIgnoreCase);
            foreach (var rule in _rules)
            {
                // Later entries override earlier ones
                _ruleCache[rule.Id] = ResolveLevel(rule);
            }
        }
    }

    /// <summary>
    /// Internal constructor for <see cref="WithOverrides"/> — accepts a pre-built cache directly.
    /// </summary>
    private Ruleset(
        IReadOnlyList<RulesetRule> rules,
        string? singleRuleWhitelist,
        Dictionary<string, RuleSeverityLevel>? ruleCache,
        bool whitelistMode)
    {
        _rules = rules;
        _singleRuleWhitelist = singleRuleWhitelist;
        _ruleCache = ruleCache;
        _whitelistMode = whitelistMode;
    }

    /// <summary>
    /// 指定したルールIDのみを有効にするホワイトリストルールセットを作成する。
    /// </summary>
    public static Ruleset CreateSingleRuleWhitelist(string ruleId)
    {
        return new Ruleset(null, ruleId);
    }

    [JsonPropertyName("rules")]
    public IReadOnlyList<RulesetRule> Rules => _rules;

    public static readonly Ruleset Empty = new(Array.Empty<RulesetRule>(), singleRuleWhitelist: null, whitelistMode: false);

    /// <summary>
    /// Loads ruleset from the specified path.
    /// </summary>
    /// <exception cref="FileNotFoundException">The ruleset file was not found.</exception>
    /// <exception cref="JsonException">The ruleset file contains invalid JSON.</exception>
    public static Ruleset Load(string path)
    {
        var result = TryLoad(path);
        if (!result.Success)
        {
            throw result.Exception ?? new InvalidOperationException(result.ErrorMessage);
        }
        return result.Value!;
    }

    /// <summary>
    /// Attempts to load ruleset from the specified path.
    /// </summary>
    public static ConfigLoadResult<Ruleset> TryLoad(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return ConfigLoadResult<Ruleset>.Fail(
                    $"Ruleset file not found: {path}",
                    new FileNotFoundException("Ruleset file not found.", path));
            }

            var json = File.ReadAllText(path);
            var ruleset = JsonSerializer.Deserialize<Ruleset>(json, JsonDefaults.Options);

            // Treat JSON `null` as an empty whitelist ruleset (disable all rules),
            // not as override-only mode.
            return ConfigLoadResult<Ruleset>.Ok(ruleset ?? new Ruleset([]));
        }
        catch (JsonException ex)
        {
            return ConfigLoadResult<Ruleset>.Fail(
                $"Invalid JSON in ruleset file: {ex.Message}",
                ex);
        }
        catch (IOException ex)
        {
            return ConfigLoadResult<Ruleset>.Fail(
                $"Failed to read ruleset file: {ex.Message}",
                ex);
        }
    }

    /// <summary>
    /// Checks if a rule is enabled.
    /// In whitelist mode (ruleset file), unlisted rules are disabled.
    /// In override-only mode (<see cref="Empty"/>), unlisted rules are enabled.
    /// </summary>
    public bool IsRuleEnabled(string ruleId)
    {
        // Single rule whitelist mode: only the specified rule is enabled
        if (_singleRuleWhitelist is not null)
        {
            return string.Equals(_singleRuleWhitelist, ruleId, StringComparison.OrdinalIgnoreCase);
        }

        return GetRuleSeverityLevel(ruleId) != RuleSeverityLevel.None;
    }

    /// <summary>
    /// Gets the configured severity level for the specified rule.
    /// In whitelist mode, unlisted rules return <see cref="RuleSeverityLevel.None"/> (disabled).
    /// Otherwise, unlisted rules return <see cref="RuleSeverityLevel.Inherit"/> (enabled).
    /// </summary>
    public RuleSeverityLevel GetRuleSeverityLevel(string ruleId)
    {
        // Single-rule whitelist mode.
        if (_singleRuleWhitelist is not null)
        {
            return string.Equals(_singleRuleWhitelist, ruleId, StringComparison.OrdinalIgnoreCase)
                ? RuleSeverityLevel.Inherit
                : RuleSeverityLevel.None;
        }

        if (_ruleCache is null)
        {
            // Empty ruleset file in whitelist mode means "disable all rules".
            return _whitelistMode ? RuleSeverityLevel.None : RuleSeverityLevel.Inherit;
        }

        if (_ruleCache.TryGetValue(ruleId, out var level))
        {
            return level;
        }

        // Whitelist mode: unlisted rules are disabled.
        return _whitelistMode ? RuleSeverityLevel.None : RuleSeverityLevel.Inherit;
    }

    /// <summary>
    /// Gets the severity override for the specified rule.
    /// Returns <c>null</c> if no severity override is configured (i.e. Inherit, None, or not present).
    /// </summary>
    public DiagnosticSeverity? GetSeverityOverride(string ruleId)
    {
        var level = GetRuleSeverityLevel(ruleId);
        return level switch
        {
            RuleSeverityLevel.Error => DiagnosticSeverity.Error,
            RuleSeverityLevel.Warning => DiagnosticSeverity.Warning,
            RuleSeverityLevel.Info => DiagnosticSeverity.Information,
            _ => null
        };
    }

    /// <summary>
    /// Creates a new Ruleset with plugin rule IDs added as defaults.
    /// In whitelist mode, plugin rules are added with <see cref="RuleSeverityLevel.Inherit"/>
    /// (enabled with default severity) unless already present in the cache.
    /// In non-whitelist mode, this is a no-op since all rules are enabled by default.
    /// </summary>
    public Ruleset WithPluginDefaults(IEnumerable<string> pluginRuleIds)
    {
        if (_singleRuleWhitelist is not null)
        {
            return this;
        }

        if (!_whitelistMode)
        {
            return this;
        }

        var merged = _ruleCache is not null
            ? new Dictionary<string, RuleSeverityLevel>(_ruleCache, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, RuleSeverityLevel>(StringComparer.OrdinalIgnoreCase);

        var changed = false;
        foreach (var ruleId in pluginRuleIds)
        {
            if (merged.TryAdd(ruleId, RuleSeverityLevel.Inherit))
            {
                changed = true;
            }
        }

        return changed
            ? new Ruleset(_rules, _singleRuleWhitelist, merged, _whitelistMode)
            : this;
    }

    /// <summary>
    /// Creates a new Ruleset by merging config-level rule overrides on top of this instance.
    /// </summary>
    public Ruleset WithOverrides(IReadOnlyDictionary<string, string> overrides)
    {
        var merged = _ruleCache is not null
            ? new Dictionary<string, RuleSeverityLevel>(_ruleCache, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, RuleSeverityLevel>(StringComparer.OrdinalIgnoreCase);

        foreach (var (ruleId, severityStr) in overrides)
        {
            merged[ruleId] = ParseSeverityLevel(severityStr);
        }

        return new Ruleset(_rules, _singleRuleWhitelist, merged, _whitelistMode);
    }

    /// <summary>
    /// Parses a severity level string into a <see cref="RuleSeverityLevel"/>.
    /// </summary>
    /// <param name="value">The severity string. Null or empty defaults to <see cref="RuleSeverityLevel.Inherit"/>.</param>
    /// <returns>The parsed severity level.</returns>
    /// <exception cref="ConfigValidationException">Thrown for unrecognized severity values.</exception>
    internal static RuleSeverityLevel ParseSeverityLevel(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return RuleSeverityLevel.Inherit;
        }

        return value.ToLowerInvariant() switch
        {
            "none" => RuleSeverityLevel.None,
            "inherit" => RuleSeverityLevel.Inherit,
            "error" => RuleSeverityLevel.Error,
            "warning" => RuleSeverityLevel.Warning,
            "info" => RuleSeverityLevel.Info,
            _ => throw new ConfigValidationException(
                $"Invalid severity '{value}'. Valid values: error, warning, info, inherit, none.")
        };
    }

    private static RuleSeverityLevel ResolveLevel(RulesetRule rule) =>
        ParseSeverityLevel(rule.Severity);
}
