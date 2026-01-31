using TsqlRefine.Cli;

namespace TsqlRefine.Cli.Tests;

public sealed class CliHelpTests
{
    [Fact]
    public async Task Version_WithShortFlag_DisplaysVersionInfo()
    {
        using var stdin = new MemoryStream();
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = await CliApp.RunAsync(new[] { "-v" }, stdin, stdout, stderr);

        Assert.Equal(0, code);
        Assert.Contains("tsqlrefine", stdout.ToString());
    }

    [Fact]
    public async Task ListRules_ReturnsSuccess()
    {
        // Verify list-rules command works, which is one of the commands mentioned in help
        using var stdin = new MemoryStream();
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = await CliApp.RunAsync(new[] { "list-rules" }, stdin, stdout, stderr);

        Assert.Equal(0, code);
        Assert.Contains("avoid-select-star", stdout.ToString());
    }

    [Fact]
    public async Task ListPlugins_ReturnsSuccess()
    {
        // Verify list-plugins command works
        using var stdin = new MemoryStream();
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = await CliApp.RunAsync(new[] { "list-plugins" }, stdin, stdout, stderr);

        Assert.Equal(0, code);
    }
}
