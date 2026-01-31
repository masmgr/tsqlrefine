using System.Text.Json;
using TsqlRefine.Cli;

namespace TsqlRefine.Cli.Tests;

public sealed class CliSeverityFilterTests
{
    [Fact]
    public async Task Lint_WithSeverityError_FiltersWarnings()
    {
        // SELECT * is a warning by default
        var stdin = new StringReader("SELECT * FROM t;");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = await CliApp.RunAsync(new[] { "lint", "--stdin", "--severity", "error", "--output", "json" }, stdin, stdout, stderr);

        // With severity=error, warnings should be filtered out
        using var doc = JsonDocument.Parse(stdout.ToString());
        var diagnostics = doc.RootElement.GetProperty("files")[0].GetProperty("diagnostics");

        // All remaining diagnostics should be errors
        foreach (var diag in diagnostics.EnumerateArray())
        {
            var severity = diag.GetProperty("severity").GetString();
            Assert.Equal("Error", severity);
        }
    }

    [Fact]
    public async Task Lint_WithSeverityWarning_IncludesWarnings()
    {
        var stdin = new StringReader("SELECT * FROM t;");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = await CliApp.RunAsync(new[] { "lint", "--stdin", "--severity", "warning", "--output", "json" }, stdin, stdout, stderr);

        using var doc = JsonDocument.Parse(stdout.ToString());
        var diagnostics = doc.RootElement.GetProperty("files")[0].GetProperty("diagnostics");

        // Should include warnings (like avoid-select-star)
        Assert.True(diagnostics.GetArrayLength() > 0);
    }

    [Fact]
    public async Task Lint_WithSeverityHint_IncludesAll()
    {
        var stdin = new StringReader("SELECT * FROM t;");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = await CliApp.RunAsync(new[] { "lint", "--stdin", "--severity", "hint", "--output", "json" }, stdin, stdout, stderr);

        using var doc = JsonDocument.Parse(stdout.ToString());
        var diagnostics = doc.RootElement.GetProperty("files")[0].GetProperty("diagnostics");

        // Hint is the lowest severity, so all diagnostics should be included
        Assert.True(diagnostics.GetArrayLength() >= 1);
    }

    [Fact]
    public async Task Lint_DefaultSeverity_IsWarning()
    {
        // By default, severity should be warning level
        var stdin = new StringReader("SELECT * FROM t;");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = await CliApp.RunAsync(new[] { "lint", "--stdin", "--output", "json" }, stdin, stdout, stderr);

        using var doc = JsonDocument.Parse(stdout.ToString());
        var diagnostics = doc.RootElement.GetProperty("files")[0].GetProperty("diagnostics");

        // Should include the warning for SELECT *
        Assert.Contains(diagnostics.EnumerateArray(), d =>
            d.GetProperty("code").GetString() == "avoid-select-star");
    }
}
