namespace TsqlRefine.Cli.Tests;

public sealed class SchemaCollectionParseErrorTests
{
    [Theory]
    [InlineData("collect-relations", "relations.json")]
    [InlineData("collect-objects", "objects.json")]
    public async Task Collect_InvalidSql_ReturnsAnalysisErrorWithoutWritingArtifact(
        string command,
        string outputFileName)
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"tsqlrefine-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
        try
        {
            var inputPath = Path.Combine(tempDirectory, "bad.sql");
            var outputPath = Path.Combine(tempDirectory, outputFileName);
            await File.WriteAllTextAsync(inputPath, "SELECT * FROM");
            var stderr = new StringWriter();

            var code = await CliApp.RunAsync(
                ["schema", command, "--output", outputPath, inputPath],
                TextReader.Null,
                new StringWriter(),
                stderr);

            Assert.Equal(ExitCodes.AnalysisError, code);
            Assert.False(File.Exists(outputPath));
            Assert.Contains("Parse error", stderr.ToString(), StringComparison.Ordinal);
            Assert.Contains("bad.sql(", stderr.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }
}
