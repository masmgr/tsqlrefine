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
        Assert.True(firstRule.TryGetProperty("fixable", out _));
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
}
