using TsqlRefine.Cli;

namespace TsqlRefine.Cli.Tests;

public sealed class CliFixTests
{
    [Fact]
    public async Task Fix_WhenNoViolations_Exit0()
    {
        var stdin = new StringReader("SELECT id FROM dbo.users;");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = await CliApp.RunAsync(new[] { "fix", "--stdin" }, stdin, stdout, stderr);

        Assert.Equal(0, code);
    }

    [Fact]
    public async Task Fix_OutputsFixedText()
    {
        var stdin = new StringReader("SELECT id FROM dbo.users;");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        await CliApp.RunAsync(new[] { "fix", "--stdin" }, stdin, stdout, stderr);

        var output = stdout.ToString();
        Assert.Contains("SELECT", output);
    }

    [Fact]
    public async Task Fix_WithDiff_RequiresDiffOrWriteForMultipleFiles()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            Directory.CreateDirectory(tempDir);
            await File.WriteAllTextAsync(Path.Combine(tempDir, "file1.sql"), "SELECT 1;");
            await File.WriteAllTextAsync(Path.Combine(tempDir, "file2.sql"), "SELECT 2;");

            var stdout = new StringWriter();
            var stderr = new StringWriter();

            var code = await CliApp.RunAsync(new[] { "fix", tempDir }, TextReader.Null, stdout, stderr);

            Assert.Equal(ExitCodes.Fatal, code);
            Assert.Contains("--write", stderr.ToString());
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task Fix_WithDiffFlag_Succeeds()
    {
        // Currently no rules are fixable, so there should be no diff output for rule violations
        // But the command should execute successfully (exit 0 when no unfixable violations, or 1 when violations remain)
        using var stdin = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("SELECT id FROM t;"));
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = await CliApp.RunAsync(new[] { "fix", "--stdin", "--diff" }, stdin, stdout, stderr);

        // Should be 0 (no violations) or 1 (violations remain after fix)
        Assert.True(code == 0 || code == ExitCodes.Violations);
    }

    [Fact]
    public async Task Fix_DiffAndWriteMutuallyExclusive()
    {
        var stdin = new StringReader("SELECT 1;");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = await CliApp.RunAsync(new[] { "fix", "--stdin", "--diff", "--write" }, stdin, stdout, stderr);

        Assert.Equal(ExitCodes.Fatal, code);
        Assert.Contains("mutually exclusive", stderr.ToString());
    }
}
