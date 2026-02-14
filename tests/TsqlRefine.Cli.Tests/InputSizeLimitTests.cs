using System.Text;

namespace TsqlRefine.Cli.Tests;

public sealed class InputSizeLimitTests
{
    [Fact]
    public void Parse_MaxFileSizeOption_ParsesCorrectly()
    {
        var args = CliParser.Parse(["lint", "--stdin", "--max-file-size", "5"]);
        Assert.Equal(5L * 1024 * 1024, args.MaxFileSize);
    }

    [Fact]
    public void Parse_MaxFileSizeDefault_Is10MB()
    {
        var args = CliParser.Parse(["lint", "--stdin"]);
        Assert.Equal(10L * 1024 * 1024, args.MaxFileSize);
    }

    [Fact]
    public void Parse_MaxFileSizeInvalid_FallsBackToDefault()
    {
        var args = CliParser.Parse(["lint", "--stdin", "--max-file-size", "abc"]);
        Assert.Equal(10L * 1024 * 1024, args.MaxFileSize);
    }

    [Fact]
    public async Task Lint_FileExceedsMaxSize_SkipsFileWithWarning()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            // Create a file slightly over 1MB
            var sqlPath = Path.Combine(tempDir, "large.sql");
            var content = new string('X', 1_100_000);
            await File.WriteAllTextAsync(sqlPath, $"SELECT '{content}';", Encoding.UTF8);

            using var stdin = new MemoryStream();
            var stdout = new StringWriter();
            var stderr = new StringWriter();

            var code = await CliApp.RunAsync(
                ["lint", "--max-file-size", "1", sqlPath],
                stdin, stdout, stderr);

            var stderrOutput = stderr.ToString();
            Assert.Contains("Skipped", stderrOutput);
            Assert.Contains("exceeds maximum", stderrOutput);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Lint_StdinExceedsMaxSize_ReportsError()
    {
        // Create stdin content over 1MB
        var largeContent = new string('X', 1_100_000);
        using var stdin = new MemoryStream(Encoding.UTF8.GetBytes($"SELECT '{largeContent}';"));
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = await CliApp.RunAsync(
            ["lint", "--stdin", "--max-file-size", "1"],
            stdin, stdout, stderr);

        var stderrOutput = stderr.ToString();
        Assert.Contains("exceeds maximum", stderrOutput);
    }
}
