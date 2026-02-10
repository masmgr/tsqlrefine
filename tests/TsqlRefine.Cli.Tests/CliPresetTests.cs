namespace TsqlRefine.Cli.Tests;

public sealed class CliPresetTests
{
    [Fact]
    public async Task Lint_WithPresetRecommended_UsesRuleset()
    {
        var stdin = new StringReader("SELECT * FROM t;");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        // Presets are resolved from AppContext.BaseDirectory (build output has rulesets/)
        var code = await CliApp.RunAsync(new[] { "lint", "--stdin", "--preset", "recommended" }, stdin, stdout, stderr);

        // Just verify it ran without config error
        Assert.NotEqual(ExitCodes.ConfigError, code);
    }

    [Fact]
    public async Task Lint_WithNonexistentPreset_ReturnsConfigError()
    {
        var stdin = new StringReader("SELECT 1;");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = await CliApp.RunAsync(new[] { "lint", "--stdin", "--preset", "nonexistent" }, stdin, stdout, stderr);

        Assert.Equal(ExitCodes.ConfigError, code);
    }

    [Fact]
    public async Task Lint_WithNonexistentPreset_ShowsAvailablePresets()
    {
        var stdin = new StringReader("SELECT 1;");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = await CliApp.RunAsync(new[] { "lint", "--stdin", "--preset", "nonexistent" }, stdin, stdout, stderr);

        Assert.Equal(ExitCodes.ConfigError, code);
        var stderrOutput = stderr.ToString();
        Assert.Contains("Unknown preset: 'nonexistent'", stderrOutput);
        Assert.Contains("Available presets:", stderrOutput);
        // Real preset files from build output
        Assert.Contains("recommended", stderrOutput);
        Assert.Contains("strict", stderrOutput);
    }
}
