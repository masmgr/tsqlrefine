namespace TsqlRefine.Cli.Tests;

public class CliVersionTests
{
    [Fact]
    public async Task Version_WhenRequested_ShowsVersionNumber()
    {
        var stdin = new StringReader("");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        // --version is a global option that works without a command
        var code = await CliApp.RunAsync(new[] { "--version" }, stdin, stdout, stderr);

        Assert.Equal(0, code);
        var output = stdout.ToString().Trim();
        // System.CommandLine outputs just the version number (e.g., "0.1.0-alpha+hash")
        Assert.Matches(@"^\d+\.\d+\.\d+", output);
    }

    [Fact]
    public async Task Version_ContainsNumericVersion_MatchesPattern()
    {
        var stdin = new StringReader("");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        // --version works as a global option
        var code = await CliApp.RunAsync(new[] { "--version" }, stdin, stdout, stderr);

        Assert.Equal(0, code);
        var output = stdout.ToString();
        // Matches semantic version pattern (e.g., "0.1.0" or "0.1.0-alpha+hash")
        Assert.Matches(@"\d+\.\d+\.\d+", output);
    }
}
