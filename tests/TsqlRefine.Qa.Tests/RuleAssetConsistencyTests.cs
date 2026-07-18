using System.Text.Json;
using TsqlRefine.Rules;

namespace TsqlRefine.Qa.Tests;

public sealed class RuleAssetConsistencyTests
{
    [Fact]
    public void EveryBuiltinRule_HasDocumentationSqlExamplesAndPresetAssignment()
    {
        var presetRuleIds = Directory.EnumerateFiles(Path.Combine(CorpusSupport.RepositoryRoot, "rulesets"), "*.json")
            .SelectMany(path =>
            {
                using var document = JsonDocument.Parse(File.ReadAllText(path));
                return document.RootElement.GetProperty("rules").EnumerateArray()
                    .Select(rule => rule.GetProperty("id").GetString()!)
                    .ToArray();
            })
            .ToHashSet(StringComparer.Ordinal);
        var failures = new List<string>();

        foreach (var rule in new BuiltinRuleProvider().GetRules())
        {
            var metadata = rule.Metadata;
            var docPath = Path.Combine(
                CorpusSupport.RepositoryRoot,
                "docs",
                "Rules",
                metadata.Category.ToLowerInvariant(),
                metadata.RuleId.Replace('/', '-') + ".md");
            if (!File.Exists(docPath))
            {
                failures.Add($"{metadata.RuleId}: missing documentation");
            }
            else if (!File.ReadAllText(docPath).Contains("```sql", StringComparison.OrdinalIgnoreCase))
            {
                failures.Add($"{metadata.RuleId}: documentation has no SQL sample");
            }

            if (!presetRuleIds.Contains(metadata.RuleId))
            {
                failures.Add($"{metadata.RuleId}: not assigned to any preset");
            }
        }

        Assert.Empty(failures);
    }
}
