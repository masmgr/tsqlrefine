using TsqlRefine.Cli;

namespace TsqlRefine.Cli.Tests;

public sealed class CliFormatTests
{
    [Fact]
    public async Task Format_OutputsToStdoutForStdin()
    {
        var stdin = new StringReader("select *\r\n\tfrom t\r\nwhere id=1  ");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = await CliApp.RunAsync(["format", "--stdin"], stdin, stdout, stderr);

        Assert.Equal(0, code);
        // Default identifier casing is None, so identifiers keep original casing
        Assert.Equal("SELECT *\n    FROM t\nWHERE id=1\n", stdout.ToString());
        Assert.Equal(string.Empty, stderr.ToString());
    }

    [Fact]
    public async Task Format_DoesNotChangeStringsOrComments()
    {
        var stdin = new StringReader("select '--select' as s -- select\nselect [from] from t;\n");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = await CliApp.RunAsync(["format", "--stdin"], stdin, stdout, stderr);

        Assert.Equal(0, code);
        // Default identifier casing is None, so aliases, tables, columns keep original casing
        Assert.Equal("SELECT '--select' AS s -- select\nSELECT [from] FROM t;\n", stdout.ToString());
        Assert.Equal(string.Empty, stderr.ToString());
    }

    [Fact]
    public async Task Format_WritesToFileWhenFileInput()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"tsqlrefine-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var sqlPath = Path.Combine(tempDir, "sample.sql");
            await File.WriteAllTextAsync(sqlPath, "select * from t");

            var stdout = new StringWriter();
            var stderr = new StringWriter();

            var code = await CliApp.RunAsync(["format", sqlPath], TextReader.Null, stdout, stderr);

            Assert.Equal(0, code);
            // stdout should be empty (file output, not stdout)
            Assert.Empty(stdout.ToString());
            // stderr should contain the change log
            Assert.Contains("Formatted:", stderr.ToString());
            // File should be updated (default identifier casing is None)
            var content = await File.ReadAllTextAsync(sqlPath);
            Assert.Equal("SELECT * FROM t\n", content);
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
    public async Task Format_UsesEditorConfigIndentForSqlFiles()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"tsqlrefine-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var editorConfigPath = Path.Combine(tempDir, ".editorconfig");
            var sqlPath = Path.Combine(tempDir, "sample.sql");

            await File.WriteAllTextAsync(editorConfigPath, "root = true\n[*.sql]\nindent_style = tab\nindent_size = 2\n");
            await File.WriteAllTextAsync(sqlPath, "select *\n    from t");

            var stdout = new StringWriter();
            var stderr = new StringWriter();

            var code = await CliApp.RunAsync(["format", sqlPath], TextReader.Null, stdout, stderr);

            Assert.Equal(0, code);
            // stdout should be empty (file output, not stdout)
            Assert.Empty(stdout.ToString());
            // stderr should contain the change log
            Assert.Contains("Formatted:", stderr.ToString());
            // File should be updated with tab indentation (default identifier casing is None)
            var content = await File.ReadAllTextAsync(sqlPath);
            Assert.Equal("SELECT *\n\t\tFROM t\n", content);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }
}
