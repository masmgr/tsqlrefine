namespace TsqlRefine.Cli.Tests;

/// <summary>
/// Tests for the 'schema build' CLI command argument validation.
/// </summary>
[Collection("DirectoryChanging")]
public sealed class SchemaBuildCommandTests
{
    [Fact]
    public async Task SchemaBuild_MissingConnectionString_ReturnsConfigError()
    {
        var originalValue = Environment.GetEnvironmentVariable(
            CliParser.ConnectionStringEnvironmentVariable);

        try
        {
            Environment.SetEnvironmentVariable(
                CliParser.ConnectionStringEnvironmentVariable,
                null);
            var stdin = new StringReader(string.Empty);
            var stdout = new StringWriter();
            var stderr = new StringWriter();

            var code = await CliApp.RunAsync(
                ["schema", "build", "--output-dir", "some/dir"],
                stdin, stdout, stderr);

            Assert.Equal(ExitCodes.ConfigError, code);
            Assert.Contains("--connection-string", stderr.ToString());
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                CliParser.ConnectionStringEnvironmentVariable,
                originalValue);
        }
    }

    [Fact]
    public async Task SchemaBuild_MissingOutputDir_ReturnsConfigError()
    {
        var stdin = new StringReader(string.Empty);
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = await CliApp.RunAsync(
            ["schema", "build", "--connection-string", "Server=.;Database=Test;"],
            stdin, stdout, stderr);

        Assert.Equal(ExitCodes.ConfigError, code);
        Assert.Contains("--output-dir", stderr.ToString());
    }

    [Fact]
    public async Task SchemaSnapshot_CanceledToken_ReturnsFatalWithoutConnecting()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        var code = await CliApp.RunAsync(
            [
                "schema", "snapshot",
                "--connection-string", "Server=localhost;Database=Test;Integrated Security=true;",
                "--output", "schema.json"
            ],
            TextReader.Null,
            stdout,
            stderr,
            cancellationSource.Token);

        Assert.Equal(ExitCodes.Fatal, code);
        Assert.Contains("Operation canceled.", stderr.ToString());
    }
}
