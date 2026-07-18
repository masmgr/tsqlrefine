using System.Text.Json;

namespace TsqlRefine.Cli.Tests;

public sealed class SchemaCollectObjectsCommandTests
{
    [Fact]
    public async Task CollectObjects_WritesVersionedCatalog()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"tsqlrefine-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
        try
        {
            var inputPath = Path.Combine(tempDirectory, "objects.sql");
            var outputPath = Path.Combine(tempDirectory, "objects.json");
            await File.WriteAllTextAsync(
                inputPath,
                "CREATE PROCEDURE dbo.Ping @value int AS SELECT @value;");
            var stdout = new StringWriter();
            var stderr = new StringWriter();

            var code = await CliApp.RunAsync(
                ["schema", "collect-objects", "--output", outputPath, inputPath],
                TextReader.Null,
                stdout,
                stderr);

            Assert.Equal(0, code);
            Assert.True(File.Exists(outputPath));
            using var document = JsonDocument.Parse(await File.ReadAllTextAsync(outputPath));
            Assert.Equal(1, document.RootElement.GetProperty("version").GetInt32());
            Assert.Single(document.RootElement.GetProperty("objects").EnumerateArray());
            Assert.Contains("1 objects", stdout.ToString());
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task CollectObjects_WithoutOutput_ReturnsConfigError()
    {
        var code = await CliApp.RunAsync(
            ["schema", "collect-objects", "--stdin"],
            new StringReader("CREATE PROCEDURE dbo.P AS SELECT 1;"),
            new StringWriter(),
            new StringWriter());

        Assert.Equal(ExitCodes.ConfigError, code);
    }
}
