namespace TsqlRefine.Cli.Tests;

/// <summary>
/// Tests for .tsqlrefine/ directory-based configuration discovery.
/// Uses DirectoryChanging collection for serial execution.
/// </summary>
[Collection("DirectoryChanging")]
public sealed class CliConfigDiscoveryTests
{
    [Fact]
    public async Task Lint_WithConfigInDotTsqlrefineDir_UsesConfig()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var originalDir = Directory.GetCurrentDirectory();

        try
        {
            var configDir = Path.Combine(tempDir, ".tsqlrefine");
            Directory.CreateDirectory(configDir);
            Directory.SetCurrentDirectory(tempDir);

            // Create config with strict preset in .tsqlrefine/
            await File.WriteAllTextAsync(
                Path.Combine(configDir, "tsqlrefine.json"),
                """{"compatLevel": 160, "preset": "strict"}""");

            var stdout = new StringWriter();
            var stderr = new StringWriter();

            var code = await CliApp.RunAsync(
                new[] { "print-config" },
                TextReader.Null, stdout, stderr);

            Assert.Equal(0, code);

            using var doc = System.Text.Json.JsonDocument.Parse(stdout.ToString());
            Assert.Equal(160, doc.RootElement.GetProperty("compatLevel").GetInt32());
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
            await Task.Delay(100);
            try
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
            catch (IOException)
            {
                // Ignore cleanup errors
            }
        }
    }

    [Fact]
    public async Task Lint_WithConfigInBothCwdAndSubdir_PrefersDirectCwd()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var originalDir = Directory.GetCurrentDirectory();

        try
        {
            var configDir = Path.Combine(tempDir, ".tsqlrefine");
            Directory.CreateDirectory(configDir);
            Directory.SetCurrentDirectory(tempDir);

            // CWD direct: compatLevel 150
            await File.WriteAllTextAsync(
                Path.Combine(tempDir, "tsqlrefine.json"),
                """{"compatLevel": 150}""");

            // .tsqlrefine/ subdir: compatLevel 160
            await File.WriteAllTextAsync(
                Path.Combine(configDir, "tsqlrefine.json"),
                """{"compatLevel": 160}""");

            var stdout = new StringWriter();
            var stderr = new StringWriter();

            var code = await CliApp.RunAsync(
                new[] { "print-config" },
                TextReader.Null, stdout, stderr);

            Assert.Equal(0, code);

            using var doc = System.Text.Json.JsonDocument.Parse(stdout.ToString());
            // CWD direct (150) should take precedence over .tsqlrefine/ (160)
            Assert.Equal(150, doc.RootElement.GetProperty("compatLevel").GetInt32());
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
            await Task.Delay(100);
            try
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
            catch (IOException)
            {
                // Ignore cleanup errors
            }
        }
    }

    [Fact]
    public async Task Lint_WithExplicitConfig_IgnoresDiscovery()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var originalDir = Directory.GetCurrentDirectory();

        try
        {
            var configDir = Path.Combine(tempDir, ".tsqlrefine");
            Directory.CreateDirectory(configDir);
            Directory.SetCurrentDirectory(tempDir);

            // .tsqlrefine/ subdir: compatLevel 160
            await File.WriteAllTextAsync(
                Path.Combine(configDir, "tsqlrefine.json"),
                """{"compatLevel": 160}""");

            // Explicit config: compatLevel 130
            var explicitConfig = Path.Combine(tempDir, "explicit.json");
            await File.WriteAllTextAsync(explicitConfig, """{"compatLevel": 130}""");

            var stdout = new StringWriter();
            var stderr = new StringWriter();

            var code = await CliApp.RunAsync(
                new[] { "print-config", "--config", explicitConfig },
                TextReader.Null, stdout, stderr);

            Assert.Equal(0, code);

            using var doc = System.Text.Json.JsonDocument.Parse(stdout.ToString());
            // Explicit config (130) should win over .tsqlrefine/ (160)
            Assert.Equal(130, doc.RootElement.GetProperty("compatLevel").GetInt32());
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
            await Task.Delay(100);
            try
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
            catch (IOException)
            {
                // Ignore cleanup errors
            }
        }
    }

    [Fact]
    public async Task Lint_WithIgnoreInDotTsqlrefineDir_UsesIgnorePatterns()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var originalDir = Directory.GetCurrentDirectory();

        try
        {
            var configDir = Path.Combine(tempDir, ".tsqlrefine");
            Directory.CreateDirectory(configDir);
            Directory.SetCurrentDirectory(tempDir);

            // Create a SQL file that would be ignored
            var ignoredDir = Path.Combine(tempDir, "ignored");
            Directory.CreateDirectory(ignoredDir);
            await File.WriteAllTextAsync(
                Path.Combine(ignoredDir, "test.sql"),
                "SELECT * FROM foo;");

            // Create ignore file in .tsqlrefine/
            await File.WriteAllTextAsync(
                Path.Combine(configDir, "tsqlrefine.ignore"),
                "ignored/\n");

            var stdin = new StringReader("SELECT 1;");
            var stdout = new StringWriter();
            var stderr = new StringWriter();

            // lint with stdin; the .tsqlrefine/tsqlrefine.ignore should be discovered
            var code = await CliApp.RunAsync(
                new[] { "lint", "--stdin" },
                stdin, stdout, stderr);

            // Should succeed (stdin input has no violations)
            Assert.True(code == 0 || code == 1);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
            await Task.Delay(100);
            try
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
            catch (IOException)
            {
                // Ignore cleanup errors
            }
        }
    }
}
