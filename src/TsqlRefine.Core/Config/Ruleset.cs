using System.Text.Json;
using System.Text.Json.Serialization;

namespace TsqlRefine.Core.Config;

public sealed record RulesetRule(string Id, bool Enabled = true);

public sealed class Ruleset
{
    private readonly IReadOnlyList<RulesetRule> _rules;
    private readonly Dictionary<string, bool>? _ruleCache;
    private readonly string? _singleRuleWhitelist;

    [JsonConstructor]
    public Ruleset(IReadOnlyList<RulesetRule>? rules) : this(rules, null)
    {
    }

    private Ruleset(IReadOnlyList<RulesetRule>? rules, string? singleRuleWhitelist)
    {
        _rules = rules ?? Array.Empty<RulesetRule>();
        _singleRuleWhitelist = singleRuleWhitelist;

        // Pre-build cache for O(1) lookup
        if (_rules.Count > 0)
        {
            _ruleCache = new Dictionary<string, bool>(_rules.Count, StringComparer.OrdinalIgnoreCase);
            foreach (var rule in _rules)
            {
                // Later entries override earlier ones
                _ruleCache[rule.Id] = rule.Enabled;
            }
        }
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

    public static readonly Ruleset Empty = new(Array.Empty<RulesetRule>());

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

            return ConfigLoadResult<Ruleset>.Ok(ruleset ?? Empty);
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
    /// Checks if a rule is enabled. Returns true by default if the rule is not in the ruleset.
    /// </summary>
    public bool IsRuleEnabled(string ruleId)
    {
        // Single rule whitelist mode: only the specified rule is enabled
        if (_singleRuleWhitelist is not null)
        {
            return string.Equals(_singleRuleWhitelist, ruleId, StringComparison.OrdinalIgnoreCase);
        }

        if (_ruleCache is null)
        {
            return true; // No rules defined, all rules enabled by default
        }

        return _ruleCache.TryGetValue(ruleId, out var enabled) ? enabled : true;
    }
}

