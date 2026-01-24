using System.Text.Json;
using TsqlRefine.Cli;

namespace TsqlRefine.Cli.Tests;

public sealed class CliLintTests
{
    [Fact]
    public async Task Lint_WhenViolation_JsonAndExit1()
    {
        var stdin = new StringReader("select * from t;");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = await CliApp.RunAsync(new[] { "lint", "--stdin", "--output", "json" }, stdin, stdout, stderr);

        Assert.Equal(ExitCodes.Violations, code);

        using var doc = JsonDocument.Parse(stdout.ToString());
        Assert.Equal("tsqlrefine", doc.RootElement.GetProperty("tool").GetString());
        Assert.Equal("lint", doc.RootElement.GetProperty("command").GetString());
    }

    [Fact]
    public async Task Lint_WhenNoViolation_Exit0()
    {
        var stdin = new StringReader("select id from t;");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = await CliApp.RunAsync(new[] { "lint", "--stdin", "--output", "json" }, stdin, stdout, stderr);

        Assert.Equal(0, code);
    }
}

