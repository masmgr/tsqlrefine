using System.Text;

namespace TsqlRefine.Cli.Tests;

public sealed class PluginLoadDiagnosticsTests
{
    [Fact]
    public async Task ListPlugins_WithDisabledPlugin_ShowsDisabledStatus()
    {
        // Create a temporary config file with a disabled plugin
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var configPath = Path.Combine(tempDir, "tsqlrefine.json");
            var configContent = """
                {
                    "compatLevel": 150,
                    "plugins": [
                        { "path": "disabled-plugin.dll", "enabled": false }
                    ]
                }
                """;
            await File.WriteAllTextAsync(configPath, configContent, Encoding.UTF8);

            using var stdin = new MemoryStream();
            var stdout = new StringWriter();
            var stderr = new StringWriter();

            // Use --config to specify config path instead of changing directory
            var code = await CliApp.RunAsync(new[] { "list-plugins", "--config", configPath }, stdin, stdout, stderr);

            Assert.Equal(0, code);
            var output = stdout.ToString();
            Assert.Contains("disabled-plugin.dll", output);
            Assert.Contains("Disabled", output);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ListPlugins_WithMissingPlugin_ShowsFileNotFoundStatus()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var configPath = Path.Combine(tempDir, "tsqlrefine.json");
            var configContent = """
                {
                    "compatLevel": 150,
                    "plugins": [
                        { "path": "missing-plugin.dll", "enabled": true }
                    ]
                }
                """;
            await File.WriteAllTextAsync(configPath, configContent, Encoding.UTF8);

            using var stdin = new MemoryStream();
            var stdout = new StringWriter();
            var stderr = new StringWriter();

            // Use --config to specify config path instead of changing directory
            var code = await CliApp.RunAsync(new[] { "list-plugins", "--config", configPath }, stdin, stdout, stderr);

            Assert.Equal(0, code);
            var output = stdout.ToString();
            Assert.Contains("missing-plugin.dll", output);
            Assert.Contains("FileNotFound", output);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }
}
