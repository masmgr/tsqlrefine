using TsqlRefine.Cli.Services;

namespace TsqlRefine.Cli.Tests;

public sealed class InputReaderParallelTests
{
    [Fact]
    public async Task ReadInputsAsync_MultipleFilesAndMissingPath_PreservesOrderAndContinues()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"tsqlrefine-input-reader-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
        try
        {
            var firstPath = Path.Combine(tempDirectory, "first.sql");
            var missingPath = Path.Combine(tempDirectory, "missing.sql");
            var secondPath = Path.Combine(tempDirectory, "second.sql");
            await File.WriteAllTextAsync(firstPath, "SELECT 1;");
            await File.WriteAllTextAsync(secondPath, "SELECT 2;");
            var args = CliParser.Parse(["lint", firstPath, missingPath, secondPath]);
            var stderr = new StringWriter();

            var result = await new InputReader().ReadInputsAsync(args, new StringReader(""), [], stderr);

            Assert.Equal([firstPath, secondPath], result.Inputs.Select(input => input.FilePath));
            Assert.Equal(["SELECT 1;", "SELECT 2;"], result.Inputs.Select(input => input.Text));
            Assert.Contains($"File not found: {missingPath}", stderr.ToString(), StringComparison.Ordinal);
            Assert.Equal(2, result.WriteEncodings.Count);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }
}
