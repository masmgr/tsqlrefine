namespace TsqlRefine.Cli.Tests;

/// <summary>
/// Init command changes current directory, so tests need isolation.
/// Uses DirectoryChanging collection for serial execution.
/// </summary>
[Collection("DirectoryChanging")]
public sealed class CliInitTests
{
    [Fact]
    public async Task Init_WhenNoConfigExists_CreatesFilesInDotTsqlrefineDir()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var originalDir = Directory.GetCurrentDirectory();

        try
        {
            Directory.CreateDirectory(tempDir);
            Directory.SetCurrentDirectory(tempDir);

            using var stdin = new MemoryStream();
            var stdout = new StringWriter();
            var stderr = new StringWriter();

            var code = await CliApp.RunAsync(new[] { "init" }, stdin, stdout, stderr);

            Assert.Equal(0, code);
            Assert.True(File.Exists(Path.Combine(tempDir, ".tsqlrefine", "tsqlrefine.json")));
            Assert.True(File.Exists(Path.Combine(tempDir, ".tsqlrefine", "tsqlrefine.ignore")));
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
    public async Task Init_WhenConfigExists_ReturnsFatal()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var originalDir = Directory.GetCurrentDirectory();

        try
        {
            var configDir = Path.Combine(tempDir, ".tsqlrefine");
            Directory.CreateDirectory(configDir);
            Directory.SetCurrentDirectory(tempDir);

            await File.WriteAllTextAsync(Path.Combine(configDir, "tsqlrefine.json"), "{}");

            using var stdin = new MemoryStream();
            var stdout = new StringWriter();
            var stderr = new StringWriter();

            var code = await CliApp.RunAsync(new[] { "init" }, stdin, stdout, stderr);

            Assert.Equal(ExitCodes.Fatal, code);
            Assert.Contains("already exist", stderr.ToString());
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
    public async Task Init_CreatesValidJsonConfig()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var originalDir = Directory.GetCurrentDirectory();

        try
        {
            Directory.CreateDirectory(tempDir);
            Directory.SetCurrentDirectory(tempDir);

            using var stdin = new MemoryStream();
            await CliApp.RunAsync(new[] { "init" }, stdin, new StringWriter(), new StringWriter());

            var configContent = await File.ReadAllTextAsync(Path.Combine(tempDir, ".tsqlrefine", "tsqlrefine.json"));
            var doc = System.Text.Json.JsonDocument.Parse(configContent);

            Assert.True(doc.RootElement.TryGetProperty("compatLevel", out _));
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
    public async Task Init_CreatesConfigWithSchemaReference()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var originalDir = Directory.GetCurrentDirectory();

        try
        {
            Directory.CreateDirectory(tempDir);
            Directory.SetCurrentDirectory(tempDir);

            using var stdin = new MemoryStream();
            await CliApp.RunAsync(new[] { "init" }, stdin, new StringWriter(), new StringWriter());

            var configContent = await File.ReadAllTextAsync(Path.Combine(tempDir, ".tsqlrefine", "tsqlrefine.json"));
            var doc = System.Text.Json.JsonDocument.Parse(configContent);

            Assert.True(doc.RootElement.TryGetProperty("$schema", out var schema));
            Assert.Equal("../schemas/tsqlrefine.schema.json", schema.GetString());
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
    public async Task Init_OutputsSuccessMessage()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var originalDir = Directory.GetCurrentDirectory();

        try
        {
            Directory.CreateDirectory(tempDir);
            Directory.SetCurrentDirectory(tempDir);

            using var stdin = new MemoryStream();
            var stdout = new StringWriter();
            var stderr = new StringWriter();

            var code = await CliApp.RunAsync(new[] { "init" }, stdin, stdout, stderr);

            Assert.Equal(0, code);
            var output = stdout.ToString();
            Assert.Contains("Created", output);
            Assert.Contains("tsqlrefine.json", output);
            Assert.Contains("tsqlrefine.ignore", output);
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
    public async Task Init_Force_OverwritesExistingFiles()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var originalDir = Directory.GetCurrentDirectory();

        try
        {
            var configDir = Path.Combine(tempDir, ".tsqlrefine");
            Directory.CreateDirectory(configDir);
            Directory.SetCurrentDirectory(tempDir);

            // Create existing config
            await File.WriteAllTextAsync(Path.Combine(configDir, "tsqlrefine.json"), "{}");

            using var stdin = new MemoryStream();
            var stdout = new StringWriter();
            var stderr = new StringWriter();

            var code = await CliApp.RunAsync(new[] { "init", "--force" }, stdin, stdout, stderr);

            Assert.Equal(0, code);
            var configContent = await File.ReadAllTextAsync(Path.Combine(configDir, "tsqlrefine.json"));
            Assert.Contains("compatLevel", configContent);
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
    public async Task Init_WithPreset_UsesSpecifiedPreset()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var originalDir = Directory.GetCurrentDirectory();

        try
        {
            Directory.CreateDirectory(tempDir);
            Directory.SetCurrentDirectory(tempDir);

            using var stdin = new MemoryStream();
            var stdout = new StringWriter();
            var stderr = new StringWriter();

            var code = await CliApp.RunAsync(new[] { "init", "--preset", "strict" }, stdin, stdout, stderr);

            Assert.Equal(0, code);
            var configContent = await File.ReadAllTextAsync(Path.Combine(tempDir, ".tsqlrefine", "tsqlrefine.json"));
            Assert.Contains("\"preset\": \"strict\"", configContent);
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
    public async Task Init_WithCompatLevel_UsesSpecifiedLevel()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var originalDir = Directory.GetCurrentDirectory();

        try
        {
            Directory.CreateDirectory(tempDir);
            Directory.SetCurrentDirectory(tempDir);

            using var stdin = new MemoryStream();
            var stdout = new StringWriter();
            var stderr = new StringWriter();

            var code = await CliApp.RunAsync(new[] { "init", "--compat-level", "160" }, stdin, stdout, stderr);

            Assert.Equal(0, code);
            var configContent = await File.ReadAllTextAsync(Path.Combine(tempDir, ".tsqlrefine", "tsqlrefine.json"));
            var doc = System.Text.Json.JsonDocument.Parse(configContent);
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
    public async Task Init_WhenConfigExists_ErrorSuggestsForce()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var originalDir = Directory.GetCurrentDirectory();

        try
        {
            var configDir = Path.Combine(tempDir, ".tsqlrefine");
            Directory.CreateDirectory(configDir);
            Directory.SetCurrentDirectory(tempDir);

            await File.WriteAllTextAsync(Path.Combine(configDir, "tsqlrefine.json"), "{}");

            using var stdin = new MemoryStream();
            var stdout = new StringWriter();
            var stderr = new StringWriter();

            var code = await CliApp.RunAsync(new[] { "init" }, stdin, stdout, stderr);

            Assert.Equal(ExitCodes.Fatal, code);
            Assert.Contains("--force", stderr.ToString());
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
    public async Task Init_CreatesDotTsqlrefineDirectory()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var originalDir = Directory.GetCurrentDirectory();

        try
        {
            Directory.CreateDirectory(tempDir);
            Directory.SetCurrentDirectory(tempDir);

            using var stdin = new MemoryStream();
            var stdout = new StringWriter();
            var stderr = new StringWriter();

            var code = await CliApp.RunAsync(new[] { "init" }, stdin, stdout, stderr);

            Assert.Equal(0, code);
            Assert.True(Directory.Exists(Path.Combine(tempDir, ".tsqlrefine")));
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
