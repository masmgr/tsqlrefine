using System.Text.Json;

namespace TsqlRefine.Cli.Tests;

public sealed class CliListRulesTests
{
    [Fact]
    public async Task ListRules_OutputsAllBuiltinRules()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = await CliApp.RunAsync(new[] { "list-rules" }, TextReader.Null, stdout, stderr);

        Assert.Equal(0, code);
        var output = stdout.ToString();
        Assert.Contains("avoid-select-star", output);
    }

    [Fact]
    public async Task ListRules_IncludesTableHeader()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        await CliApp.RunAsync(new[] { "list-rules" }, TextReader.Null, stdout, stderr);

        var output = stdout.ToString();
        Assert.Contains("Rule ID", output);
        Assert.Contains("Category", output);
        Assert.Contains("Severity", output);
        Assert.Contains("Fixable", output);
        Assert.Contains("Enabled", output);
        Assert.Contains("Total:", output);
    }

    [Fact]
    public async Task ListRules_WithJsonOutput_ReturnsValidJson()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = await CliApp.RunAsync(new[] { "list-rules", "--output", "json" }, TextReader.Null, stdout, stderr);

        Assert.Equal(0, code);

        using var doc = JsonDocument.Parse(stdout.ToString());
        var rules = doc.RootElement.EnumerateArray().ToList();
        Assert.True(rules.Count > 0);

        var firstRule = rules[0];
        Assert.True(firstRule.TryGetProperty("id", out _));
        Assert.True(firstRule.TryGetProperty("description", out _));
        Assert.True(firstRule.TryGetProperty("category", out _));
        Assert.True(firstRule.TryGetProperty("defaultSeverity", out _));
        Assert.True(firstRule.TryGetProperty("effectiveSeverity", out _));
        Assert.True(firstRule.TryGetProperty("fixable", out _));
        Assert.True(firstRule.TryGetProperty("enabled", out _));
        Assert.True(firstRule.TryGetProperty("documentationUri", out var docUri));
        Assert.StartsWith("https://github.com/masmgr/tsqlrefine/blob/main/docs/Rules/", docUri.GetString());
    }

    [Fact]
    public async Task ListRules_JsonOutputContainsExpectedRule()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        await CliApp.RunAsync(new[] { "list-rules", "--output", "json" }, TextReader.Null, stdout, stderr);

        using var doc = JsonDocument.Parse(stdout.ToString());
        var rules = doc.RootElement.EnumerateArray().ToList();

        Assert.Contains(rules, r => r.GetProperty("id").GetString() == "avoid-select-star");
    }

    [Fact]
    public async Task ListRules_WithCategoryFilter_FiltersRules()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        await CliApp.RunAsync(new[] { "list-rules", "--category", "Security" }, TextReader.Null, stdout, stderr);

        var output = stdout.ToString();
        Assert.Contains("Security", output);
        // Security rules should not include performance-only rules
        Assert.DoesNotContain("avoid-select-star", output);
    }

    [Fact]
    public async Task ListRules_WithFixableFilter_ShowsOnlyFixableRules()
    {
        var stdoutAll = new StringWriter();
        var stdoutFixable = new StringWriter();

        await CliApp.RunAsync(new[] { "list-rules" }, TextReader.Null, stdoutAll, new StringWriter());
        await CliApp.RunAsync(new[] { "list-rules", "--fixable" }, TextReader.Null, stdoutFixable, new StringWriter());

        // Fixable list should be shorter than full list
        var allLines = stdoutAll.ToString().Split('\n').Length;
        var fixableLines = stdoutFixable.ToString().Split('\n').Length;
        Assert.True(fixableLines < allLines);

        // All listed rules should contain "Yes" in the Fixable column
        var lines = stdoutFixable.ToString().Split('\n')
            .Select(l => l.TrimEnd())
            .Where(l => l.Length > 0 && !l.StartsWith("Rule ID") && !l.StartsWith("\u2500") && !l.StartsWith("Total:"))
            .ToArray();
        Assert.True(lines.Length > 0);
        foreach (var line in lines)
        {
            Assert.Contains("Yes", line);
        }
    }

    [Fact]
    public async Task ListRules_WithoutConfig_AppliesRecommendedPreset()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = await CliApp.RunAsync(new[] { "list-rules", "--output", "json" }, TextReader.Null, stdout, stderr);

        Assert.Equal(0, code);
        using var doc = JsonDocument.Parse(stdout.ToString());
        var rules = doc.RootElement.EnumerateArray().ToList();

        // Default applies recommended preset — some rules enabled, some disabled
        var enabledCount = rules.Count(r => r.GetProperty("enabled").GetBoolean());
        var disabledCount = rules.Count(r => !r.GetProperty("enabled").GetBoolean());
        Assert.True(enabledCount > 0, "recommended preset should enable some rules");
        Assert.True(disabledCount > 0, "recommended preset should disable some rules");
    }

    [Fact]
    public async Task ListRules_WithoutConfig_EnabledRulesHaveValidSeverity()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        await CliApp.RunAsync(new[] { "list-rules", "--output", "json" }, TextReader.Null, stdout, stderr);

        using var doc = JsonDocument.Parse(stdout.ToString());
        var rules = doc.RootElement.EnumerateArray().ToList();

        // Enabled rules: effective severity should match default severity (recommended preset uses inherit)
        Assert.All(
            rules.Where(r => r.GetProperty("enabled").GetBoolean()),
            r => Assert.Equal(
                r.GetProperty("defaultSeverity").GetString(),
                r.GetProperty("effectiveSeverity").GetString()));

        // Disabled rules: effective severity should be "none"
        Assert.All(
            rules.Where(r => !r.GetProperty("enabled").GetBoolean()),
            r => Assert.Equal("none", r.GetProperty("effectiveSeverity").GetString()));
    }

    [Fact]
    public async Task ListRules_WithPreset_ShowsDisabledRules()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        // security-only preset enables very few rules; unlisted rules are disabled (whitelist mode)
        var code = await CliApp.RunAsync(
            new[] { "list-rules", "--preset", "security-only", "--output", "json" },
            TextReader.Null, stdout, stderr);

        Assert.Equal(0, code);
        using var doc = JsonDocument.Parse(stdout.ToString());
        var rules = doc.RootElement.EnumerateArray().ToList();

        // Should have some disabled rules (majority are not in security-only)
        var disabledCount = rules.Count(r => !r.GetProperty("enabled").GetBoolean());
        Assert.True(disabledCount > 0, "security-only preset should disable rules not in its list");

        // Should have some enabled rules
        var enabledCount = rules.Count(r => r.GetProperty("enabled").GetBoolean());
        Assert.True(enabledCount > 0, "security-only preset should enable some rules");

        // Disabled rules should report effectiveSeverity as none.
        Assert.All(
            rules.Where(r => !r.GetProperty("enabled").GetBoolean()),
            r => Assert.Equal("none", r.GetProperty("effectiveSeverity").GetString()));
    }
}
