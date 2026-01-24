using System.Text.Json;

namespace TsqlRefine.Core.Config;

public sealed record RulesetRule(string Id, bool Enabled = true);

public sealed record Ruleset(IReadOnlyList<RulesetRule> Rules)
{
    public static Ruleset Load(string path)
    {
        var json = File.ReadAllText(path);
        var ruleset = JsonSerializer.Deserialize<Ruleset>(json, JsonDefaults.Options);
        return ruleset ?? new Ruleset(Array.Empty<RulesetRule>());
    }

    public bool IsRuleEnabled(string ruleId)
    {
        var match = Rules.FirstOrDefault(r => string.Equals(r.Id, ruleId, StringComparison.OrdinalIgnoreCase));
        return match?.Enabled ?? true;
    }
}

