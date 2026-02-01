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

        var code = await CliApp.RunAsync(["fix", "--stdin"], stdin, stdout, stderr);

        Assert.Equal(0, code);
    }

    [Fact]
    public async Task Fix_OutputsFixedTextToStdoutForStdin()
    {
        var stdin = new StringReader("SELECT id FROM dbo.users;");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        await CliApp.RunAsync(["fix", "--stdin"], stdin, stdout, stderr);

        var output = stdout.ToString();
        Assert.Contains("SELECT", output);
    }

    [Fact]
    public async Task Fix_WritesToFileWhenFileInput()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            Directory.CreateDirectory(tempDir);
            var filePath = Path.Combine(tempDir, "test.sql");
            await File.WriteAllTextAsync(filePath, "SELECT 1;");

            var stdout = new StringWriter();
            var stderr = new StringWriter();

            var code = await CliApp.RunAsync(["fix", filePath], TextReader.Null, stdout, stderr);

            Assert.Equal(0, code);
            // stdout should be empty (file output, not stdout)
            Assert.Empty(stdout.ToString());
            // File should still contain the SQL
            var content = await File.ReadAllTextAsync(filePath);
            Assert.Contains("SELECT", content);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task Fix_WritesToMultipleFilesWithoutWriteOption()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            Directory.CreateDirectory(tempDir);
            var file1 = Path.Combine(tempDir, "file1.sql");
            var file2 = Path.Combine(tempDir, "file2.sql");
            await File.WriteAllTextAsync(file1, "SELECT 1;");
            await File.WriteAllTextAsync(file2, "SELECT 2;");

            var stdout = new StringWriter();
            var stderr = new StringWriter();

            var code = await CliApp.RunAsync(["fix", tempDir], TextReader.Null, stdout, stderr);

            Assert.Equal(0, code);
            // stdout should be empty
            Assert.Empty(stdout.ToString());
            // Files should be updated
            Assert.Contains("SELECT", await File.ReadAllTextAsync(file1));
            Assert.Contains("SELECT", await File.ReadAllTextAsync(file2));
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task Fix_WhenUnfixableViolationsRemain_Exit0()
    {
        // SELECT * は avoid-select-star ルールに違反するが、このルールは Fixable=false
        var stdin = new StringReader("SELECT * FROM dbo.users;");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = await CliApp.RunAsync(["fix", "--stdin"], stdin, stdout, stderr);

        // 修正できない違反が残っていても、fix コマンド自体は成功扱い
        Assert.Equal(0, code);
    }
}
