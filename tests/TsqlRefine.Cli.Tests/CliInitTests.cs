using TsqlRefine.Cli;

namespace TsqlRefine.Cli.Tests;

// Init command changes current directory, so tests need isolation
public sealed class CliInitTests
{
    private static readonly SemaphoreSlim _directoryLock = new(1, 1);

    [Fact]
    public async Task Init_WhenNoConfigExists_CreatesFiles()
    {
        await _directoryLock.WaitAsync();
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
            Assert.True(File.Exists(Path.Combine(tempDir, "tsqlrefine.json")));
            Assert.True(File.Exists(Path.Combine(tempDir, "tsqlrefine.ignore")));
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
            // Wait a bit for file handles to be released
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
            _directoryLock.Release();
        }
    }

    [Fact]
    public async Task Init_WhenConfigExists_ReturnsFatal()
    {
        await _directoryLock.WaitAsync();
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var originalDir = Directory.GetCurrentDirectory();

        try
        {
            Directory.CreateDirectory(tempDir);
            Directory.SetCurrentDirectory(tempDir);

            await File.WriteAllTextAsync(Path.Combine(tempDir, "tsqlrefine.json"), "{}");

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
            _directoryLock.Release();
        }
    }

    [Fact]
    public async Task Init_CreatesValidJsonConfig()
    {
        await _directoryLock.WaitAsync();
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var originalDir = Directory.GetCurrentDirectory();

        try
        {
            Directory.CreateDirectory(tempDir);
            Directory.SetCurrentDirectory(tempDir);

            using var stdin = new MemoryStream();
            await CliApp.RunAsync(new[] { "init" }, stdin, new StringWriter(), new StringWriter());

            var configContent = await File.ReadAllTextAsync(Path.Combine(tempDir, "tsqlrefine.json"));
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
            _directoryLock.Release();
        }
    }
}
