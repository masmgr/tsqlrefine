using System.Text.Json;
using TsqlRefine.Cli;

namespace TsqlRefine.Cli.Tests;

public sealed class CliParseErrorTests
{
    [Fact]
    public async Task Lint_WhenSyntaxError_ReturnsAnalysisError()
    {
        var stdin = new StringReader("SELECT * FROM");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = await CliApp.RunAsync(new[] { "lint", "--stdin" }, stdin, stdout, stderr);

        Assert.Equal(ExitCodes.AnalysisError, code);
    }

    [Fact]
    public async Task Lint_WhenSyntaxError_JsonContainsParseError()
    {
        var stdin = new StringReader("SELECT * FROM");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        await CliApp.RunAsync(new[] { "lint", "--stdin", "--output", "json" }, stdin, stdout, stderr);

        using var doc = JsonDocument.Parse(stdout.ToString());
        var files = doc.RootElement.GetProperty("files");
        Assert.True(files.GetArrayLength() > 0);

        var diagnostics = files[0].GetProperty("diagnostics");
        Assert.Contains(diagnostics.EnumerateArray(), d =>
            d.GetProperty("code").GetString() == "parse-error");
    }

    [Fact]
    public async Task Lint_WhenSyntaxError_TextOutputContainsParseError()
    {
        var stdin = new StringReader("SELECT * FROM");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        await CliApp.RunAsync(new[] { "lint", "--stdin" }, stdin, stdout, stderr);

        var output = stdout.ToString();
        Assert.Contains("Parse error", output);
    }

    [Fact]
    public async Task Fix_WhenSyntaxError_ReturnsAnalysisError()
    {
        var stdin = new StringReader("SELECT * FROM");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = await CliApp.RunAsync(new[] { "fix", "--stdin" }, stdin, stdout, stderr);

        Assert.Equal(ExitCodes.AnalysisError, code);
    }

    [Fact]
    public async Task Lint_WhenValidSql_DoesNotReturnAnalysisError()
    {
        var stdin = new StringReader("SELECT id FROM dbo.users;");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = await CliApp.RunAsync(new[] { "lint", "--stdin" }, stdin, stdout, stderr);

        Assert.NotEqual(ExitCodes.AnalysisError, code);
    }
}
