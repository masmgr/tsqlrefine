using System.Text.RegularExpressions;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Tests;

public sealed partial class BuiltinRuleConventionTests
{
    [Fact]
    public void AllPublicRuleImplementations_HaveCorrespondingTestClass()
    {
        var ruleTypes = typeof(BuiltinRuleProvider).Assembly.GetTypes()
            .Where(type => type.IsClass && type.IsPublic && !type.IsAbstract && typeof(IRule).IsAssignableFrom(type))
            .ToArray();
        var testTypeNames = typeof(BuiltinRuleConventionTests).Assembly.GetTypes()
            .Where(type => type.IsClass)
            .Select(type => type.Name)
            .ToHashSet(StringComparer.Ordinal);

        var missing = ruleTypes
            .Select(type => $"{type.Name}Tests")
            .Where(expectedName => !testTypeNames.Contains(expectedName))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.True(missing.Length == 0,
            $"Missing rule test class(es):{Environment.NewLine}{string.Join(Environment.NewLine, missing)}");
    }

    [Fact]
    public void AllBuiltinRuleIds_AreUniqueKebabCase()
    {
        var ruleIds = new BuiltinRuleProvider().GetRules()
            .Select(rule => rule.Metadata.RuleId)
            .ToArray();
        var invalid = ruleIds
            .Where(ruleId => string.IsNullOrWhiteSpace(ruleId) || !KebabCaseRuleId().IsMatch(ruleId))
            .OrderBy(ruleId => ruleId, StringComparer.Ordinal)
            .ToArray();
        var duplicates = ruleIds
            .GroupBy(ruleId => ruleId, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .OrderBy(ruleId => ruleId, StringComparer.Ordinal)
            .ToArray();

        Assert.True(invalid.Length == 0,
            $"Invalid rule ID(s): {string.Join(", ", invalid)}");
        Assert.True(duplicates.Length == 0,
            $"Duplicate rule ID(s): {string.Join(", ", duplicates)}");
    }

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex KebabCaseRuleId();
}
