namespace TsqlRefine.Cli.Tests;

/// <summary>
/// Tests for CLI option validation (mutual exclusion, conflict warnings).
/// </summary>
public sealed class CliOptionValidationTests
{
    [Fact]
    public async Task Lint_PresetAndRuleset_ReturnsConfigError()
    {
        var stdin = new StringReader("SELECT 1;");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = await CliApp.RunAsync(
            ["lint", "--stdin", "--preset", "recommended", "--ruleset", "custom.json"],
            stdin, stdout, stderr);

        Assert.Equal(ExitCodes.ConfigError, code);
        Assert.Contains("mutually exclusive", stderr.ToString());
    }

    [Fact]
    public async Task Fix_PresetAndRuleset_ReturnsConfigError()
    {
        var stdin = new StringReader("SELECT 1;");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = await CliApp.RunAsync(
            ["fix", "--stdin", "--preset", "recommended", "--ruleset", "custom.json"],
            stdin, stdout, stderr);

        Assert.Equal(ExitCodes.ConfigError, code);
        Assert.Contains("mutually exclusive", stderr.ToString());
    }

    [Fact]
    public async Task Lint_VerboseAndQuiet_ReturnsConfigError()
    {
        var stdin = new StringReader("SELECT 1;");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = await CliApp.RunAsync(
            ["lint", "--stdin", "--verbose", "--quiet"],
            stdin, stdout, stderr);

        Assert.Equal(ExitCodes.ConfigError, code);
        Assert.Contains("mutually exclusive", stderr.ToString());
    }

    [Fact]
    public async Task Fix_VerboseAndQuiet_ReturnsConfigError()
    {
        var stdin = new StringReader("SELECT 1;");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = await CliApp.RunAsync(
            ["fix", "--stdin", "--verbose", "--quiet"],
            stdin, stdout, stderr);

        Assert.Equal(ExitCodes.ConfigError, code);
        Assert.Contains("mutually exclusive", stderr.ToString());
    }

    [Fact]
    public async Task Fix_RuleWithPreset_WarnsToStderr()
    {
        var stdin = new StringReader("select 1;");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        await CliApp.RunAsync(
            ["fix", "--stdin", "--rule", "keyword-casing", "--preset", "strict"],
            stdin, stdout, stderr);

        Assert.Contains("--rule overrides --preset", stderr.ToString());
    }

    [Fact]
    public async Task Fix_RuleWithRuleset_WarnsToStderr()
    {
        var stdin = new StringReader("select 1;");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        await CliApp.RunAsync(
            ["fix", "--stdin", "--rule", "keyword-casing", "--ruleset", "custom.json"],
            stdin, stdout, stderr);

        Assert.Contains("--rule overrides --ruleset", stderr.ToString());
    }

    [Fact]
    public async Task Fix_Verbose_OutputsTimeToStderr()
    {
        var stdin = new StringReader("SELECT 1;");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        await CliApp.RunAsync(
            ["fix", "--stdin", "--verbose"],
            stdin, stdout, stderr);

        Assert.Contains("Time:", stderr.ToString());
    }
}
