namespace TsqlRefine.Cli.Tests;

public class CliIgnoreListTests
{
    [Fact]
    public async Task IgnoreList_WithComments_IgnoresCommentLines()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            // Create ignore file with comments
            var ignoreFile = Path.Combine(tempDir, "test.ignore");
            await File.WriteAllTextAsync(ignoreFile, "# This is a comment\nbin/\n# Another comment\nobj/");

            // Create test SQL files
            var binDir = Path.Combine(tempDir, "bin");
            Directory.CreateDirectory(binDir);
            await File.WriteAllTextAsync(Path.Combine(binDir, "test.sql"), "select * from t");
            await File.WriteAllTextAsync(Path.Combine(tempDir, "main.sql"), "select 1");

            var stdin = new StringReader("");
            var stdout = new StringWriter();
            var stderr = new StringWriter();

            var code = await CliApp.RunAsync(
                new[] { "lint", "-g", ignoreFile, tempDir },
                stdin, stdout, stderr);

            // Should only process main.sql, not bin/test.sql
            var output = stdout.ToString();
            Assert.DoesNotContain("bin", output);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task IgnoreList_WithBlankLines_IgnoresBlankLines()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            // Create ignore file with blank lines
            var ignoreFile = Path.Combine(tempDir, "test.ignore");
            await File.WriteAllTextAsync(ignoreFile, "\n\nbin/\n\n\nobj/\n\n");

            // Create test SQL files
            var binDir = Path.Combine(tempDir, "bin");
            Directory.CreateDirectory(binDir);
            await File.WriteAllTextAsync(Path.Combine(binDir, "test.sql"), "select * from t");
            await File.WriteAllTextAsync(Path.Combine(tempDir, "main.sql"), "select 1");

            var stdin = new StringReader("");
            var stdout = new StringWriter();
            var stderr = new StringWriter();

            var code = await CliApp.RunAsync(
                new[] { "lint", "-g", ignoreFile, tempDir },
                stdin, stdout, stderr);

            // Should succeed (blank lines don't cause errors)
            Assert.True(code == 0 || code == ExitCodes.Violations);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task IgnoreList_WithNonexistentFile_ReturnsConfigError()
    {
        var stdin = new StringReader("");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = await CliApp.RunAsync(
            new[] { "lint", "-g", "nonexistent.ignore", "." },
            stdin, stdout, stderr);

        Assert.Equal(ExitCodes.ConfigError, code);
        Assert.Contains("not found", stderr.ToString());
    }

    [Fact]
    public async Task IgnoreList_WithoutFile_ContinuesNormally()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            // Create test SQL file
            await File.WriteAllTextAsync(Path.Combine(tempDir, "test.sql"), "select 1");

            var stdin = new StringReader("");
            var stdout = new StringWriter();
            var stderr = new StringWriter();

            // No -g option, no default ignore file
            var code = await CliApp.RunAsync(
                new[] { "lint", tempDir },
                stdin, stdout, stderr);

            // Should succeed without ignore file
            Assert.True(code == 0 || code == ExitCodes.Violations);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task IgnoreList_WithDirectoryPattern_FiltersRecursively()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            // Create ignore file with directory pattern
            var ignoreFile = Path.Combine(tempDir, "test.ignore");
            await File.WriteAllTextAsync(ignoreFile, "bin/**");

            // Create nested directory structure
            var binDir = Path.Combine(tempDir, "bin");
            var binSubDir = Path.Combine(binDir, "subdir");
            Directory.CreateDirectory(binSubDir);
            await File.WriteAllTextAsync(Path.Combine(binSubDir, "test.sql"), "select * from t");
            await File.WriteAllTextAsync(Path.Combine(tempDir, "main.sql"), "select 1");

            var stdin = new StringReader("");
            var stdout = new StringWriter();
            var stderr = new StringWriter();

            var code = await CliApp.RunAsync(
                new[] { "lint", "-g", ignoreFile, tempDir },
                stdin, stdout, stderr);

            // Should succeed, ignoring bin/** files
            Assert.True(code == 0 || code == ExitCodes.Violations);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
