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
    public async Task Lint_UnknownOption_ReturnsConfigError()
    {
        var stdin = new StringReader("SELECT 1;");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = await CliApp.RunAsync(
            ["lint", "--stdin", "--bogus"],
            stdin, stdout, stderr);

        Assert.Equal(ExitCodes.ConfigError, code);
        Assert.Contains("--bogus", stderr.ToString());
    }

    [Fact]
    public async Task Lint_InvalidOutput_ReturnsConfigError()
    {
        var stdin = new StringReader("SELECT 1;");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = await CliApp.RunAsync(
            ["lint", "--stdin", "--output", "xml"],
            stdin, stdout, stderr);

        Assert.Equal(ExitCodes.ConfigError, code);
        Assert.Contains("Invalid --output", stderr.ToString());
    }

    [Fact]
    public async Task ListRules_InvalidOutput_ReturnsConfigError()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = await CliApp.RunAsync(
            ["list-rules", "--output", "xml"],
            TextReader.Null, stdout, stderr);

        Assert.Equal(ExitCodes.ConfigError, code);
        Assert.Contains("Invalid --output", stderr.ToString());
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

    [Theory]
    [InlineData("--severity", "notice", "Invalid --severity")]
    [InlineData("--compat-level", "999", "Invalid --compat-level")]
    public async Task Lint_InvalidAnalysisOption_ReturnsConfigError(
        string option,
        string value,
        string expectedMessage)
    {
        var stdin = new StringReader("SELECT 1;");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = await CliApp.RunAsync(
            ["lint", "--stdin", option, value],
            stdin, stdout, stderr);

        Assert.Equal(ExitCodes.ConfigError, code);
        Assert.Contains(expectedMessage, stderr.ToString());
    }

    [Theory]
    [InlineData("--indent-style", "wide", "Invalid --indent-style")]
    [InlineData("--line-ending", "native", "Invalid --line-ending")]
    [InlineData("--indent-size", "0", "Invalid --indent-size")]
    public async Task Format_InvalidFormattingOption_ReturnsConfigError(
        string option,
        string value,
        string expectedMessage)
    {
        var stdin = new StringReader("SELECT 1;");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = await CliApp.RunAsync(
            ["format", "--stdin", option, value],
            stdin, stdout, stderr);

        Assert.Equal(ExitCodes.ConfigError, code);
        Assert.Contains(expectedMessage, stderr.ToString());
    }
}
