using System.Text.Json;
using System.Text.RegularExpressions;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules;

namespace TsqlRefine.Qa.Tests;

public sealed class RuleAssetConsistencyTests
{
    private static readonly Regex KebabCaseRuleId = new(
        "^[a-z0-9]+(?:-[a-z0-9]+)*(?:/[a-z0-9]+(?:-[a-z0-9]+)*)?$",
        RegexOptions.CultureInvariant);

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

    [Fact]
    public void EveryConcreteRule_IsRegisteredExactlyOnce()
    {
        var registeredRuleIds = new BuiltinRuleProvider().GetRules()
            .Select(rule => rule.Metadata.RuleId)
            .ToArray();
        var discoverableRuleIds = CreateDiscoverableRules()
            .Select(rule => rule.Metadata.RuleId)
            .ToArray();

        Assert.Equal(
            discoverableRuleIds.Order(StringComparer.Ordinal),
            registeredRuleIds.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void EveryBuiltinRule_HasUniqueKebabCaseId()
    {
        var ruleIds = new BuiltinRuleProvider().GetRules()
            .Select(rule => rule.Metadata.RuleId)
            .ToArray();

        Assert.All(ruleIds, ruleId => Assert.Matches(KebabCaseRuleId, ruleId));
        Assert.Equal(ruleIds.Length, ruleIds.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void EveryFixableRule_ProvidesRuleSpecificGetFixesImplementation()
    {
        var failures = CreateDiscoverableRules()
            .Where(rule => rule.Metadata.Fixable)
            .Where(rule => rule.GetType().GetMethod(nameof(IRule.GetFixes))?.DeclaringType != rule.GetType())
            .Select(rule => $"{rule.Metadata.RuleId}: inherits the default GetFixes implementation")
            .ToArray();

        Assert.Empty(failures);
    }

    private static IRule[] CreateDiscoverableRules() =>
        typeof(BuiltinRuleProvider).Assembly
            .GetTypes()
            .Where(type =>
                type is { IsAbstract: false, IsClass: true, IsPublic: true } &&
                typeof(IRule).IsAssignableFrom(type) &&
                type.GetConstructor(Type.EmptyTypes) is not null)
            .Select(type => (IRule)Activator.CreateInstance(type)!)
            .ToArray();
}
