using System.Text.Json;
using TsqlRefine.Cli;

namespace TsqlRefine.Cli.Tests;

public sealed class CliMultipleFilesTests
{
    [Fact]
    public async Task Lint_MultipleFiles_ReportsAllViolations()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        try
        {
            Directory.CreateDirectory(tempDir);
            await File.WriteAllTextAsync(Path.Combine(tempDir, "file1.sql"), "SELECT * FROM t1;");
            await File.WriteAllTextAsync(Path.Combine(tempDir, "file2.sql"), "SELECT * FROM t2;");

            var stdout = new StringWriter();
            var stderr = new StringWriter();

            var code = await CliApp.RunAsync(new[] { "lint", tempDir, "--output", "json" }, TextReader.Null, stdout, stderr);

            Assert.Equal(ExitCodes.Violations, code);

            using var doc = JsonDocument.Parse(stdout.ToString());
            var files = doc.RootElement.GetProperty("files");
            Assert.Equal(2, files.GetArrayLength());
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task Format_MultipleFilesWithoutWrite_ReturnsError()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        try
        {
            Directory.CreateDirectory(tempDir);
            await File.WriteAllTextAsync(Path.Combine(tempDir, "file1.sql"), "SELECT 1;");
            await File.WriteAllTextAsync(Path.Combine(tempDir, "file2.sql"), "SELECT 2;");

            var stdout = new StringWriter();
            var stderr = new StringWriter();

            var code = await CliApp.RunAsync(new[] { "format", tempDir }, TextReader.Null, stdout, stderr);

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
    public async Task Lint_DirectoryWithNestedSqlFiles_FindsAll()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        try
        {
            Directory.CreateDirectory(tempDir);
            Directory.CreateDirectory(Path.Combine(tempDir, "subdir"));
            await File.WriteAllTextAsync(Path.Combine(tempDir, "root.sql"), "SELECT * FROM t;");
            await File.WriteAllTextAsync(Path.Combine(tempDir, "subdir", "nested.sql"), "SELECT * FROM t2;");

            using var stdin = new MemoryStream();
            var stdout = new StringWriter();
            var stderr = new StringWriter();

            var code = await CliApp.RunAsync(new[] { "lint", tempDir, "--output", "json" }, stdin, stdout, stderr);

            using var doc = JsonDocument.Parse(stdout.ToString());
            var files = doc.RootElement.GetProperty("files");
            Assert.Equal(2, files.GetArrayLength());
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }
}
