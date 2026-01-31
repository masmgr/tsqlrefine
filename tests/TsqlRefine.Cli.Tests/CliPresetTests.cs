using TsqlRefine.Cli;

namespace TsqlRefine.Cli.Tests;

public sealed class CliPresetTests
{
    [Fact]
    public async Task Lint_WithPresetRecommended_UsesRuleset()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var originalDir = Directory.GetCurrentDirectory();

        try
        {
            Directory.CreateDirectory(tempDir);
            Directory.SetCurrentDirectory(tempDir);

            // Create rulesets directory with recommended.json
            var rulesetsDir = Path.Combine(tempDir, "rulesets");
            Directory.CreateDirectory(rulesetsDir);
            await File.WriteAllTextAsync(
                Path.Combine(rulesetsDir, "recommended.json"),
                """{"rules": []}""");

            var stdin = new StringReader("SELECT * FROM t;");
            var stdout = new StringWriter();
            var stderr = new StringWriter();

            // Should not throw even with preset
            var code = await CliApp.RunAsync(new[] { "lint", "--stdin", "--preset", "recommended" }, stdin, stdout, stderr);

            // Just verify it ran without config error
            Assert.NotEqual(ExitCodes.ConfigError, code);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task Lint_WithNonexistentPreset_ReturnsConfigError()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var originalDir = Directory.GetCurrentDirectory();

        try
        {
            Directory.CreateDirectory(tempDir);
            Directory.SetCurrentDirectory(tempDir);

            var stdin = new StringReader("SELECT 1;");
            var stdout = new StringWriter();
            var stderr = new StringWriter();

            var code = await CliApp.RunAsync(new[] { "lint", "--stdin", "--preset", "nonexistent" }, stdin, stdout, stderr);

            Assert.Equal(ExitCodes.ConfigError, code);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }
}
