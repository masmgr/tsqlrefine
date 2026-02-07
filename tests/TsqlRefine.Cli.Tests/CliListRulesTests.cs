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
    public async Task ListRules_IncludesRuleMetadata()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        await CliApp.RunAsync(new[] { "list-rules" }, TextReader.Null, stdout, stderr);

        var output = stdout.ToString();
        // Should include rule ID, category, severity, and fixable info
        Assert.Contains("\t", output); // Tab-separated format
        Assert.Contains("fixable=", output);
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
}
