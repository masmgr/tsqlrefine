namespace TsqlRefine.Cli.Tests;

/// <summary>
/// Tests for the 'schema build' CLI command argument validation.
/// </summary>
public sealed class SchemaBuildCommandTests
{
    [Fact]
    public async Task SchemaBuild_MissingConnectionString_ReturnsConfigError()
    {
        var stdin = new StringReader(string.Empty);
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = await CliApp.RunAsync(
            ["schema", "build", "--output-dir", "some/dir"],
            stdin, stdout, stderr);

        Assert.Equal(ExitCodes.ConfigError, code);
        Assert.Contains("--connection-string", stderr.ToString());
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
}
