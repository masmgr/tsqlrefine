using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Tests;

public sealed class RuleMetadataDocumentationUriTests
{
    private const string BaseDocUrl = "https://github.com/masmgr/tsqlrefine/blob/main/docs/Rules/";

    [Fact]
    public void DocumentationUri_SimpleRuleId_ReturnsExpectedUri()
    {
        var metadata = new RuleMetadata(
            RuleId: "avoid-select-star",
            Description: "Avoid SELECT *",
            Category: "Performance",
            DefaultSeverity: RuleSeverity.Warning,
            Fixable: false
        );

        Assert.Equal(
            new Uri($"{BaseDocUrl}performance/avoid-select-star.md"),
            metadata.DocumentationUri);
    }

    [Fact]
    public void DocumentationUri_SemanticRuleId_ReplacesSlashWithHyphen()
    {
        var metadata = new RuleMetadata(
            RuleId: "semantic/undefined-alias",
            Description: "Detects undefined aliases",
            Category: "Correctness",
            DefaultSeverity: RuleSeverity.Error,
            Fixable: false
        );

        Assert.Equal(
            new Uri($"{BaseDocUrl}correctness/semantic-undefined-alias.md"),
            metadata.DocumentationUri);
    }

    [Fact]
    public void DocumentationUri_CategoryIsCaseInsensitive()
    {
        var metadata = new RuleMetadata(
            RuleId: "test-rule",
            Description: "Test",
            Category: "Style",
            DefaultSeverity: RuleSeverity.Information,
            Fixable: false
        );

        Assert.Contains("/style/", metadata.DocumentationUri.ToString());
    }

    [Fact]
    public void DocumentationUri_AllBuiltinRules_AreNotNull()
    {
        var provider = new BuiltinRuleProvider();
        var rules = provider.GetRules();

        foreach (var rule in rules)
        {
            Assert.NotNull(rule.Metadata.DocumentationUri);
        }
    }

    [Fact]
    public void DocumentationUri_AllBuiltinRules_MatchExpectedPattern()
    {
        var provider = new BuiltinRuleProvider();
        var rules = provider.GetRules();

        foreach (var rule in rules)
        {
            var expected = BaseDocUrl
                + rule.Metadata.Category.ToLowerInvariant() + "/"
                + rule.Metadata.RuleId.Replace('/', '-') + ".md";

            Assert.Equal(expected, rule.Metadata.DocumentationUri.ToString());
        }
    }

    [Fact]
    public void DocumentationUri_AllBuiltinRules_HaveCorrespondingDocFile()
    {
        var repoRoot = FindRepoRoot();
        var provider = new BuiltinRuleProvider();
        var rules = provider.GetRules();
        var missing = new List<string>();

        foreach (var rule in rules)
        {
            var category = rule.Metadata.Category.ToLowerInvariant();
            var docFileName = rule.Metadata.RuleId.Replace('/', '-') + ".md";
            var docPath = Path.Combine(repoRoot, "docs", "Rules", category, docFileName);

            if (!File.Exists(docPath))
            {
                missing.Add($"{rule.Metadata.RuleId} -> {docPath}");
            }
        }

        Assert.True(missing.Count == 0,
            $"Missing documentation files for {missing.Count} rule(s):\n{string.Join("\n", missing)}");
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir, ".git")))
                return dir;
            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new InvalidOperationException("Could not find repository root (.git directory).");
    }
}
