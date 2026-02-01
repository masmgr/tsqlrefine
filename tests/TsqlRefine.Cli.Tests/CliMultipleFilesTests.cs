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
    public async Task Format_MultipleFiles_WritesToAllFiles()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        try
        {
            Directory.CreateDirectory(tempDir);
            var file1 = Path.Combine(tempDir, "file1.sql");
            var file2 = Path.Combine(tempDir, "file2.sql");
            await File.WriteAllTextAsync(file1, "select 1;");
            await File.WriteAllTextAsync(file2, "select 2;");

            var stdout = new StringWriter();
            var stderr = new StringWriter();

            var code = await CliApp.RunAsync(["format", tempDir], TextReader.Null, stdout, stderr);

            Assert.Equal(0, code);
            // stdout should be empty (file output, not stdout)
            Assert.Empty(stdout.ToString());
            Assert.Empty(stderr.ToString());

            // Verify files are formatted
            Assert.Contains("SELECT 1", await File.ReadAllTextAsync(file1));
            Assert.Contains("SELECT 2", await File.ReadAllTextAsync(file2));
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
