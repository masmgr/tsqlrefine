using System.Text.Json;

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
        var stdin = new StringReader("select id from dbo.t;");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = await CliApp.RunAsync(new[] { "lint", "--stdin", "--output", "json" }, stdin, stdout, stderr);

        Assert.Equal(0, code);
    }

    [Fact]
    public async Task Lint_TextOutput_IncludesLocationInfo()
    {
        var stdin = new StringReader("SELECT * FROM t;");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        await CliApp.RunAsync(new[] { "lint", "--stdin" }, stdin, stdout, stderr);

        var output = stdout.ToString();
        // Format: <stdin>:line:col: Severity: Message (rule-id)
        // SELECT * has * at position 7 (0-based), so 1:8 in 1-based
        Assert.Matches(@"<stdin>:\d+:\d+:", output);
        Assert.Contains("Warning", output);
        Assert.Contains("avoid-select-star", output);
    }

    [Fact]
    public async Task Lint_TextOutput_ShowsCorrectPosition()
    {
        // "SELECT * FROM t;" - the asterisk (*) is at column 8 (1-based)
        var stdin = new StringReader("SELECT * FROM t;");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        await CliApp.RunAsync(new[] { "lint", "--stdin" }, stdin, stdout, stderr);

        var output = stdout.ToString();
        // The * is at position 7 (0-based), which is column 8 (1-based)
        Assert.Contains("<stdin>:1:8:", output);
    }

    [Fact]
    public async Task Lint_TextOutput_MultilineShowsCorrectLines()
    {
        var sql = "SELECT\n    *\nFROM t;";
        var stdin = new StringReader(sql);
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        await CliApp.RunAsync(new[] { "lint", "--stdin" }, stdin, stdout, stderr);

        var output = stdout.ToString();
        // The * is on line 2 (1-based), column 5 (after 4 spaces)
        Assert.Contains("<stdin>:2:5:", output);
    }

    [Fact]
    public async Task Lint_AlwaysOutputsSummaryToStderr()
    {
        var stdin = new StringReader("SELECT id FROM dbo.t;");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        await CliApp.RunAsync(new[] { "lint", "--stdin" }, stdin, stdout, stderr);

        var stderrOutput = stderr.ToString();
        Assert.Contains("No problems found in 1 file.", stderrOutput);
    }

    [Fact]
    public async Task Lint_Summary_ShowsProblemCount()
    {
        // This SQL has violations: SELECT * and missing schema prefix
        var stdin = new StringReader("SELECT * FROM unqualified_table;");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        await CliApp.RunAsync(new[] { "lint", "--stdin" }, stdin, stdout, stderr);

        var stderrOutput = stderr.ToString();
        Assert.Matches(@"\d+ problems? \(", stderrOutput);
        Assert.Contains("in 1 file.", stderrOutput);
    }

    [Fact]
    public async Task Lint_Verbose_OutputsTimeToStderr()
    {
        var stdin = new StringReader("SELECT id FROM dbo.t;");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        await CliApp.RunAsync(new[] { "lint", "--stdin", "--verbose" }, stdin, stdout, stderr);

        var stderrOutput = stderr.ToString();
        Assert.Contains("No problems found in 1 file.", stderrOutput);
        Assert.Contains("Time:", stderrOutput);
    }

    [Fact]
    public async Task Lint_WithoutVerbose_DoesNotOutputTime()
    {
        var stdin = new StringReader("SELECT * FROM t;");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        await CliApp.RunAsync(new[] { "lint", "--stdin" }, stdin, stdout, stderr);

        var stderrOutput = stderr.ToString();
        Assert.DoesNotContain("Time:", stderrOutput);
    }

    [Fact]
    public async Task DefaultCommand_WithoutLint_RunsLint()
    {
        var stdin = new StringReader("SELECT * FROM t;");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        // No "lint" subcommand - should default to lint
        var code = await CliApp.RunAsync(new[] { "--stdin" }, stdin, stdout, stderr);

        Assert.Equal(ExitCodes.Violations, code);
        var output = stdout.ToString();
        Assert.Contains("avoid-select-star", output);
    }

    [Fact]
    public async Task DefaultCommand_WithJsonOutput_RunsLint()
    {
        var stdin = new StringReader("SELECT * FROM t;");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = await CliApp.RunAsync(new[] { "--stdin", "--output", "json" }, stdin, stdout, stderr);

        Assert.Equal(ExitCodes.Violations, code);
        using var doc = JsonDocument.Parse(stdout.ToString());
        Assert.Equal("tsqlrefine", doc.RootElement.GetProperty("tool").GetString());
        Assert.Equal("lint", doc.RootElement.GetProperty("command").GetString());
    }
}

