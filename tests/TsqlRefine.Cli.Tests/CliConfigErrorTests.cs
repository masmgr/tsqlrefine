using TsqlRefine.Cli;

namespace TsqlRefine.Cli.Tests;

public sealed class CliConfigErrorTests
{
    [Fact]
    public async Task Lint_WithMissingConfigPath_ReturnsConfigError()
    {
        var stdin = new StringReader("SELECT 1;");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = await CliApp.RunAsync(new[] { "lint", "--stdin", "--config", "/nonexistent/config.json" }, stdin, stdout, stderr);

        Assert.Equal(ExitCodes.ConfigError, code);
        Assert.Contains("not found", stderr.ToString());
    }

    [Fact]
    public async Task Lint_WithInvalidJsonConfig_ReturnsConfigError()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        try
        {
            Directory.CreateDirectory(tempDir);
            var configPath = Path.Combine(tempDir, "invalid.json");
            await File.WriteAllTextAsync(configPath, "{ invalid json }");

            var stdin = new StringReader("SELECT 1;");
            var stdout = new StringWriter();
            var stderr = new StringWriter();

            var code = await CliApp.RunAsync(new[] { "lint", "--stdin", "--config", configPath }, stdin, stdout, stderr);

            Assert.Equal(ExitCodes.ConfigError, code);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task Lint_WithMissingRulesetPath_ReturnsConfigError()
    {
        var stdin = new StringReader("SELECT 1;");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = await CliApp.RunAsync(new[] { "lint", "--stdin", "--ruleset", "/nonexistent/ruleset.json" }, stdin, stdout, stderr);

        Assert.Equal(ExitCodes.ConfigError, code);
    }

    [Fact]
    public async Task Lint_WithMissingIgnoreListPath_ReturnsConfigError()
    {
        var stdin = new StringReader("SELECT 1;");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = await CliApp.RunAsync(new[] { "lint", "--stdin", "--ignorelist", "/nonexistent/ignore" }, stdin, stdout, stderr);

        Assert.Equal(ExitCodes.ConfigError, code);
    }
}
