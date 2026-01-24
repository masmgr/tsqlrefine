using System.Text.RegularExpressions;

namespace TsqlRefine.Cli.Tests;

public class CliVersionTests
{
    [Fact]
    public async Task Version_WhenRequested_ShowsVersionNumber()
    {
        var stdin = new StringReader("");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        // Use with a command since global options need a command context
        var code = await CliApp.RunAsync(new[] { "lint", "--version" }, stdin, stdout, stderr);

        Assert.Equal(0, code);
        var output = stdout.ToString();
        Assert.StartsWith("tsqlrefine ", output);
    }

    [Fact]
    public async Task Version_ContainsNumericVersion_MatchesPattern()
    {
        var stdin = new StringReader("");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = await CliApp.RunAsync(new[] { "format", "-v" }, stdin, stdout, stderr);

        Assert.Equal(0, code);
        var output = stdout.ToString();
        Assert.Matches(@"tsqlrefine \d+\.\d+\.\d+", output);
    }
}
