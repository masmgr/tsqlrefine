namespace TsqlRefine.Cli.Tests;

public class CliFormatDiffTests
{
    [Fact]
    public async Task Format_WithDiff_ShowsUnifiedDiff()
    {
        var stdin = new StringReader("select * from t");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = await CliApp.RunAsync(
            new[] { "format", "--stdin", "--diff" },
            stdin, stdout, stderr);

        Assert.Equal(0, code);
        var output = stdout.ToString();
        Assert.Contains("---", output);
        Assert.Contains("+++", output);
        Assert.Contains("-select * from t", output);
        Assert.Contains("+SELECT * FROM t", output);
    }

    [Fact]
    public async Task Format_WithDiffNoChanges_OutputsNothing()
    {
        // Already formatted SQL
        var stdin = new StringReader("SELECT *\n    FROM t\nWHERE id = 1");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = await CliApp.RunAsync(
            new[] { "format", "--stdin", "--diff" },
            stdin, stdout, stderr);

        Assert.Equal(0, code);
        var output = stdout.ToString();
        Assert.Empty(output);
    }

    [Fact]
    public async Task Format_WithDiffAndWrite_ReturnsError()
    {
        var stdin = new StringReader("select * from t");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = await CliApp.RunAsync(
            new[] { "format", "--stdin", "--diff", "--write" },
            stdin, stdout, stderr);

        Assert.Equal(ExitCodes.Fatal, code);
        Assert.Contains("mutually exclusive", stderr.ToString());
    }

    [Fact]
    public async Task Format_WithDiff_ReturnsZeroExitCode()
    {
        var stdin = new StringReader("select * from t");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = await CliApp.RunAsync(
            new[] { "format", "--stdin", "--diff" },
            stdin, stdout, stderr);

        // Should return 0 even when there are differences
        Assert.Equal(0, code);
    }
}
