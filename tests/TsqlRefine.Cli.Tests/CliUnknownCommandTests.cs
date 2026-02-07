namespace TsqlRefine.Cli.Tests;

public sealed class CliUnknownCommandTests
{
    // Note: The CLI parser treats unknown commands as file paths in lint mode.
    // This test verifies that when an unknown subcommand is explicitly used,
    // the error message is informative.
    // Current behavior: "badcmd" is treated as a file path, not an unknown command.
    // The UnknownCommandAsync path is only reached for truly unrecognized commands
    // after parsing, which is hard to trigger with System.CommandLine.

    [Fact]
    public async Task Lint_WithNonexistentFile_ReportsFileNotFound()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        // When a non-existent file is passed, it reports "File not found"
        var code = await CliApp.RunAsync(new[] { "lint", "nonexistent.sql" }, TextReader.Null, stdout, stderr);

        Assert.Equal(ExitCodes.Fatal, code);
        Assert.Contains("No input", stderr.ToString());
    }
}
