using System.Text.Json;
using TsqlRefine.Cli;

namespace TsqlRefine.Cli.Tests;

public sealed class CliPrintConfigTests
{
    [Fact]
    public async Task PrintConfig_OutputsValidJson()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = await CliApp.RunAsync(new[] { "print-config" }, TextReader.Null, stdout, stderr);

        Assert.Equal(0, code);

        var output = stdout.ToString();
        Assert.False(string.IsNullOrWhiteSpace(output));

        using var doc = JsonDocument.Parse(output);
        Assert.True(doc.RootElement.TryGetProperty("compatLevel", out _));
    }

    [Fact]
    public async Task PrintConfig_WithCustomConfig_IncludesValues()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        try
        {
            Directory.CreateDirectory(tempDir);
            var configPath = Path.Combine(tempDir, "custom.json");
            await File.WriteAllTextAsync(configPath, """{"compatLevel": 160}""");

            var stdout = new StringWriter();
            var stderr = new StringWriter();

            var code = await CliApp.RunAsync(new[] { "print-config", "--config", configPath }, TextReader.Null, stdout, stderr);

            Assert.Equal(0, code);

            using var doc = JsonDocument.Parse(stdout.ToString());
            Assert.Equal(160, doc.RootElement.GetProperty("compatLevel").GetInt32());
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }
}
