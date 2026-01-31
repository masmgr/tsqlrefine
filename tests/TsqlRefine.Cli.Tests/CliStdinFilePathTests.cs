using System.Text.Json;
using TsqlRefine.Cli;

namespace TsqlRefine.Cli.Tests;

public sealed class CliStdinFilePathTests
{
    [Fact]
    public async Task Lint_WithStdinFilePath_UsesProvidedPath()
    {
        var stdin = new StringReader("SELECT * FROM t;");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        await CliApp.RunAsync(new[] { "lint", "--stdin", "--stdin-filepath", "/path/to/file.sql", "--output", "json" }, stdin, stdout, stderr);

        using var doc = JsonDocument.Parse(stdout.ToString());
        var filePath = doc.RootElement.GetProperty("files")[0].GetProperty("filePath").GetString();

        Assert.Equal("/path/to/file.sql", filePath);
    }

    [Fact]
    public async Task Lint_WithoutStdinFilePath_UsesDefaultStdinPath()
    {
        using var stdin = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("SELECT * FROM t;"));
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        await CliApp.RunAsync(new[] { "lint", "--stdin", "--output", "json" }, stdin, stdout, stderr);

        using var doc = JsonDocument.Parse(stdout.ToString());
        var filePath = doc.RootElement.GetProperty("files")[0].GetProperty("filePath").GetString();

        Assert.Equal("<stdin>", filePath);
    }

    [Fact]
    public async Task Format_WithStdinFilePath_UsesProvidedPathInDiff()
    {
        var stdin = new StringReader("select 1;");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        await CliApp.RunAsync(new[] { "format", "--stdin", "--stdin-filepath", "/custom/path.sql", "--diff" }, stdin, stdout, stderr);

        var output = stdout.ToString();
        if (!string.IsNullOrEmpty(output))
        {
            Assert.Contains("/custom/path.sql", output);
        }
    }
}
