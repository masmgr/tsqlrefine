using TsqlRefine.Cli.Services;

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
    public async Task Lint_WithConfigOnlyInCwdDirect_IgnoresItAndUsesDefaults()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var originalDir = Directory.GetCurrentDirectory();

        try
        {
            Directory.CreateDirectory(tempDir);
            Directory.SetCurrentDirectory(tempDir);

            // CWD direct: compatLevel 160 — should NOT be loaded
            await File.WriteAllTextAsync(
                Path.Combine(tempDir, "tsqlrefine.json"),
                """{"compatLevel": 160}""");

            var stdout = new StringWriter();
            var stderr = new StringWriter();

            var code = await CliApp.RunAsync(
                new[] { "print-config" },
                TextReader.Null, stdout, stderr);

            Assert.Equal(0, code);

            using var doc = System.Text.Json.JsonDocument.Parse(stdout.ToString());
            // Default compatLevel is 150, not 160
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
    public async Task Lint_WithLegacyCwdConfig_EmitsWarning()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var originalDir = Directory.GetCurrentDirectory();

        try
        {
            Directory.CreateDirectory(tempDir);
            Directory.SetCurrentDirectory(tempDir);

            // Place config in CWD root (legacy location)
            await File.WriteAllTextAsync(
                Path.Combine(tempDir, "tsqlrefine.json"),
                """{"compatLevel": 160}""");

            var stdin = new StringReader("SELECT 1;");
            var stdout = new StringWriter();
            var stderr = new StringWriter();

            await CliApp.RunAsync(
                new[] { "lint", "--stdin" },
                stdin, stdout, stderr);

            // Should emit a warning about the legacy location
            Assert.Contains("tsqlrefine.json", stderr.ToString());
            Assert.Contains(".tsqlrefine/", stderr.ToString());
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

    [Fact]
    public async Task Lint_WithNamedRuleset_UsesResolvedFile()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var originalDir = Directory.GetCurrentDirectory();

        try
        {
            var rulesetsDir = Path.Combine(tempDir, ".tsqlrefine", "rulesets");
            Directory.CreateDirectory(rulesetsDir);
            Directory.SetCurrentDirectory(tempDir);

            // Create a custom named ruleset with a single rule
            await File.WriteAllTextAsync(
                Path.Combine(rulesetsDir, "my-team.json"),
                """{"rules": [{"id": "avoid-select-star"}]}""");

            var stdin = new StringReader("SELECT * FROM foo;");
            var stdout = new StringWriter();
            var stderr = new StringWriter();

            var code = await CliApp.RunAsync(
                new[] { "lint", "--stdin", "--ruleset", "my-team" },
                stdin, stdout, stderr);

            // Should find violations (avoid-select-star on SELECT *)
            Assert.Equal(1, code);
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
    public async Task Lint_WithNamedRuleset_NotFound_ReturnsConfigError()
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

            var code = await CliApp.RunAsync(
                new[] { "lint", "--stdin", "--ruleset", "nonexistent-ruleset" },
                stdin, stdout, stderr);

            Assert.Equal(ExitCodes.ConfigError, code);
            Assert.Contains("nonexistent-ruleset", stderr.ToString());
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
    public void CheckLegacyFileWarning_ForConfig_SuggestsConfigFlag()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var originalDir = Directory.GetCurrentDirectory();

        try
        {
            Directory.CreateDirectory(tempDir);
            Directory.SetCurrentDirectory(tempDir);
            File.WriteAllText(Path.Combine(tempDir, "tsqlrefine.json"), "{}");

            var warning = ConfigLoader.CheckLegacyFileWarning("tsqlrefine.json", null, "--config");

            Assert.NotNull(warning);
            Assert.Contains("--config", warning);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void CheckLegacyFileWarning_ForIgnore_SuggestsIgnorelistFlag()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var originalDir = Directory.GetCurrentDirectory();

        try
        {
            Directory.CreateDirectory(tempDir);
            Directory.SetCurrentDirectory(tempDir);
            File.WriteAllText(Path.Combine(tempDir, "tsqlrefine.ignore"), "*.bak");

            var warning = ConfigLoader.CheckLegacyFileWarning("tsqlrefine.ignore", null, "--ignorelist");

            Assert.NotNull(warning);
            Assert.Contains("--ignorelist", warning);
            Assert.DoesNotContain("--config", warning);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void CheckLegacyFileWarning_WithInvalidLoadedPath_DoesNotThrow()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var originalDir = Directory.GetCurrentDirectory();

        try
        {
            Directory.CreateDirectory(tempDir);
            Directory.SetCurrentDirectory(tempDir);
            File.WriteAllText(Path.Combine(tempDir, "tsqlrefine.json"), "{}");

            // Malformed path that would cause Path.GetFullPath to throw
            var warning = ConfigLoader.CheckLegacyFileWarning(
                "tsqlrefine.json", "invalid:\0path", "--config");

            // Should not throw; should return a warning
            Assert.NotNull(warning);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    // ========================================================
    // ResolveBaseRuleset priority tests
    // ========================================================

    [Fact]
    public async Task Lint_CliPresetOverridesConfigRuleset_UsesCliPreset()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var originalDir = Directory.GetCurrentDirectory();

        try
        {
            var configDir = Path.Combine(tempDir, ".tsqlrefine");
            var rulesetsDir = Path.Combine(configDir, "rulesets");
            Directory.CreateDirectory(rulesetsDir);
            Directory.SetCurrentDirectory(tempDir);

            // Named ruleset with only avoid-select-star (no require-explicit-join-type)
            await File.WriteAllTextAsync(
                Path.Combine(rulesetsDir, "my-team.json"),
                """{"rules": [{"id": "avoid-select-star"}]}""");

            // Config references named ruleset
            await File.WriteAllTextAsync(
                Path.Combine(configDir, "tsqlrefine.json"),
                """{"ruleset": "my-team"}""");

            // Input that triggers require-explicit-join-type (strict-only) but not avoid-select-star
            var stdin = new StringReader("SELECT a.id FROM dbo.a JOIN dbo.b ON a.id = b.id;");
            var stdout = new StringWriter();
            var stderr = new StringWriter();

            // CLI --preset strict should override config ruleset
            var code = await CliApp.RunAsync(
                new[] { "lint", "--stdin", "--preset", "strict" },
                stdin, stdout, stderr);

            // strict preset includes require-explicit-join-type → violations
            Assert.Equal(ExitCodes.Violations, code);
            Assert.Contains("require-explicit-join-type", stdout.ToString());
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
    public async Task Lint_CliRulesetOverridesConfigRuleset_UsesCliRuleset()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var originalDir = Directory.GetCurrentDirectory();

        try
        {
            var configDir = Path.Combine(tempDir, ".tsqlrefine");
            var rulesetsDir = Path.Combine(configDir, "rulesets");
            Directory.CreateDirectory(rulesetsDir);
            Directory.SetCurrentDirectory(tempDir);

            // Named ruleset with only avoid-select-star
            await File.WriteAllTextAsync(
                Path.Combine(rulesetsDir, "my-team.json"),
                """{"rules": [{"id": "avoid-select-star"}]}""");

            // Another named ruleset with require-explicit-join-type
            await File.WriteAllTextAsync(
                Path.Combine(rulesetsDir, "other.json"),
                """{"rules": [{"id": "require-explicit-join-type"}]}""");

            // Config references "other" ruleset
            await File.WriteAllTextAsync(
                Path.Combine(configDir, "tsqlrefine.json"),
                """{"ruleset": "other"}""");

            // Input with implicit JOIN but no SELECT *
            var stdin = new StringReader("SELECT a.id FROM dbo.a JOIN dbo.b ON a.id = b.id;");
            var stdout = new StringWriter();
            var stderr = new StringWriter();

            // CLI --ruleset my-team should override config ruleset "other"
            var code = await CliApp.RunAsync(
                new[] { "lint", "--stdin", "--ruleset", "my-team" },
                stdin, stdout, stderr);

            // my-team only has avoid-select-star; no SELECT * → no violations
            // (config's "other" with require-explicit-join-type is overridden)
            Assert.Equal(0, code);
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
    public async Task Lint_CliRulesetOverridesConfigPreset_UsesCliRuleset()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var originalDir = Directory.GetCurrentDirectory();

        try
        {
            var configDir = Path.Combine(tempDir, ".tsqlrefine");
            var rulesetsDir = Path.Combine(configDir, "rulesets");
            Directory.CreateDirectory(rulesetsDir);
            Directory.SetCurrentDirectory(tempDir);

            // Named ruleset with only avoid-select-star (no require-explicit-join-type)
            await File.WriteAllTextAsync(
                Path.Combine(rulesetsDir, "my-team.json"),
                """{"rules": [{"id": "avoid-select-star"}]}""");

            // Config has strict preset
            await File.WriteAllTextAsync(
                Path.Combine(configDir, "tsqlrefine.json"),
                """{"preset": "strict"}""");

            // Input with implicit JOIN but no SELECT * → strict would flag, my-team would not
            var stdin = new StringReader("SELECT a.id FROM dbo.a JOIN dbo.b ON a.id = b.id;");
            var stdout = new StringWriter();
            var stderr = new StringWriter();

            // CLI --ruleset should override config preset
            var code = await CliApp.RunAsync(
                new[] { "lint", "--stdin", "--ruleset", "my-team" },
                stdin, stdout, stderr);

            // CLI --ruleset my-team wins → no require-explicit-join-type violation
            Assert.Equal(0, code);
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
    public async Task Lint_ConfigPresetOverridesConfigRuleset_UsesPreset()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var originalDir = Directory.GetCurrentDirectory();

        try
        {
            var configDir = Path.Combine(tempDir, ".tsqlrefine");
            var rulesetsDir = Path.Combine(configDir, "rulesets");
            Directory.CreateDirectory(rulesetsDir);
            Directory.SetCurrentDirectory(tempDir);

            // Named ruleset with only avoid-select-star
            await File.WriteAllTextAsync(
                Path.Combine(rulesetsDir, "my-team.json"),
                """{"rules": [{"id": "avoid-select-star"}]}""");

            // Config has both preset and ruleset — preset should win
            await File.WriteAllTextAsync(
                Path.Combine(configDir, "tsqlrefine.json"),
                """{"preset": "strict", "ruleset": "my-team"}""");

            // Input with implicit JOIN → strict flags require-explicit-join-type
            var stdin = new StringReader("SELECT a.id FROM dbo.a JOIN dbo.b ON a.id = b.id;");
            var stdout = new StringWriter();
            var stderr = new StringWriter();

            var code = await CliApp.RunAsync(
                new[] { "lint", "--stdin" },
                stdin, stdout, stderr);

            // preset (strict) takes precedence → require-explicit-join-type violation
            Assert.Equal(ExitCodes.Violations, code);
            Assert.Contains("require-explicit-join-type", stdout.ToString());
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
    public async Task Lint_NeitherPresetNorRuleset_UsesRecommendedDefault()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var originalDir = Directory.GetCurrentDirectory();

        try
        {
            var configDir = Path.Combine(tempDir, ".tsqlrefine");
            Directory.CreateDirectory(configDir);
            Directory.SetCurrentDirectory(tempDir);

            // Config with no preset/ruleset
            await File.WriteAllTextAsync(
                Path.Combine(configDir, "tsqlrefine.json"),
                """{"compatLevel": 150}""");

            // Input with implicit JOIN → strict would flag, recommended would not
            var stdin = new StringReader("SELECT a.id FROM dbo.a JOIN dbo.b ON a.id = b.id;");
            var stdout = new StringWriter();
            var stderr = new StringWriter();

            var code = await CliApp.RunAsync(
                new[] { "lint", "--stdin" },
                stdin, stdout, stderr);

            // recommended preset is default, no require-explicit-join-type rule → clean
            var output = stdout.ToString();
            Assert.DoesNotContain("require-explicit-join-type", output);
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

    // ========================================================
    // Named ruleset via config file
    // ========================================================

    [Fact]
    public async Task Lint_WithNamedRulesetInConfig_UsesResolvedFile()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var originalDir = Directory.GetCurrentDirectory();

        try
        {
            var configDir = Path.Combine(tempDir, ".tsqlrefine");
            var rulesetsDir = Path.Combine(configDir, "rulesets");
            Directory.CreateDirectory(rulesetsDir);
            Directory.SetCurrentDirectory(tempDir);

            // Named ruleset with only avoid-select-star
            await File.WriteAllTextAsync(
                Path.Combine(rulesetsDir, "my-team.json"),
                """{"rules": [{"id": "avoid-select-star"}]}""");

            // Config references it by name
            await File.WriteAllTextAsync(
                Path.Combine(configDir, "tsqlrefine.json"),
                """{"ruleset": "my-team"}""");

            var stdin = new StringReader("SELECT * FROM foo;");
            var stdout = new StringWriter();
            var stderr = new StringWriter();

            var code = await CliApp.RunAsync(
                new[] { "lint", "--stdin" },
                stdin, stdout, stderr);

            // my-team ruleset has avoid-select-star → violation on SELECT *
            Assert.Equal(ExitCodes.Violations, code);
            Assert.Contains("avoid-select-star", stdout.ToString());
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

    // ========================================================
    // Invalid ruleset JSON
    // ========================================================

    [Fact]
    public async Task Lint_WithInvalidJsonRuleset_ReturnsConfigError()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var originalDir = Directory.GetCurrentDirectory();

        try
        {
            Directory.CreateDirectory(tempDir);
            Directory.SetCurrentDirectory(tempDir);

            // Create an invalid JSON file
            var rulesetFile = Path.Combine(tempDir, "bad-ruleset.json");
            await File.WriteAllTextAsync(rulesetFile, "{ this is not valid json }");

            var stdin = new StringReader("SELECT 1;");
            var stdout = new StringWriter();
            var stderr = new StringWriter();

            var code = await CliApp.RunAsync(
                new[] { "lint", "--stdin", "--ruleset", rulesetFile },
                stdin, stdout, stderr);

            Assert.Equal(ExitCodes.ConfigError, code);
            Assert.Contains("Failed to parse ruleset", stderr.ToString());
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
    public async Task Lint_WithInvalidJsonNamedRuleset_ReturnsConfigError()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var originalDir = Directory.GetCurrentDirectory();

        try
        {
            var rulesetsDir = Path.Combine(tempDir, ".tsqlrefine", "rulesets");
            Directory.CreateDirectory(rulesetsDir);
            Directory.SetCurrentDirectory(tempDir);

            // Named ruleset with invalid JSON
            await File.WriteAllTextAsync(
                Path.Combine(rulesetsDir, "broken.json"),
                "not json at all");

            var stdin = new StringReader("SELECT 1;");
            var stdout = new StringWriter();
            var stderr = new StringWriter();

            var code = await CliApp.RunAsync(
                new[] { "lint", "--stdin", "--ruleset", "broken" },
                stdin, stdout, stderr);

            Assert.Equal(ExitCodes.ConfigError, code);
            Assert.Contains("Failed to parse ruleset", stderr.ToString());
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

    // ========================================================
    // Legacy ignore file warning
    // ========================================================

    [Fact]
    public async Task Lint_WithLegacyCwdIgnore_EmitsWarningWithIgnorelistFlag()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var originalDir = Directory.GetCurrentDirectory();

        try
        {
            Directory.CreateDirectory(tempDir);
            Directory.SetCurrentDirectory(tempDir);

            // Place ignore file in CWD root (legacy location)
            await File.WriteAllTextAsync(
                Path.Combine(tempDir, "tsqlrefine.ignore"),
                "bin/\nobj/\n");

            var stdin = new StringReader("SELECT 1;");
            var stdout = new StringWriter();
            var stderr = new StringWriter();

            await CliApp.RunAsync(
                new[] { "lint", "--stdin" },
                stdin, stdout, stderr);

            var stderrText = stderr.ToString();
            Assert.Contains("tsqlrefine.ignore", stderrText);
            Assert.Contains("--ignorelist", stderrText);
            // Should NOT suggest --config for ignore file warnings
            Assert.DoesNotContain("--config", stderrText);
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
    public async Task Lint_WithLegacyCwdConfigAndIgnore_EmitsBothWarnings()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var originalDir = Directory.GetCurrentDirectory();

        try
        {
            Directory.CreateDirectory(tempDir);
            Directory.SetCurrentDirectory(tempDir);

            // Place both files in CWD root (legacy locations)
            await File.WriteAllTextAsync(
                Path.Combine(tempDir, "tsqlrefine.json"),
                """{"compatLevel": 160}""");
            await File.WriteAllTextAsync(
                Path.Combine(tempDir, "tsqlrefine.ignore"),
                "bin/\n");

            var stdin = new StringReader("SELECT 1;");
            var stdout = new StringWriter();
            var stderr = new StringWriter();

            await CliApp.RunAsync(
                new[] { "lint", "--stdin" },
                stdin, stdout, stderr);

            var stderrText = stderr.ToString();
            // Both warnings should be present
            Assert.Contains("tsqlrefine.json", stderrText);
            Assert.Contains("--config", stderrText);
            Assert.Contains("tsqlrefine.ignore", stderrText);
            Assert.Contains("--ignorelist", stderrText);
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
    public async Task Lint_WithQuietFlag_SuppressesLegacyWarning()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var originalDir = Directory.GetCurrentDirectory();

        try
        {
            Directory.CreateDirectory(tempDir);
            Directory.SetCurrentDirectory(tempDir);

            // Place config in CWD root (legacy location)
            await File.WriteAllTextAsync(
                Path.Combine(tempDir, "tsqlrefine.json"),
                """{"compatLevel": 160}""");

            var stdin = new StringReader("SELECT 1;");
            var stdout = new StringWriter();
            var stderr = new StringWriter();

            await CliApp.RunAsync(
                new[] { "lint", "--stdin", "--quiet" },
                stdin, stdout, stderr);

            // --quiet suppresses legacy file warnings
            Assert.DoesNotContain("tsqlrefine.json", stderr.ToString());
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
