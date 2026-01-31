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
    public async Task Fix_RequiresWriteForMultipleFiles()
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
}
