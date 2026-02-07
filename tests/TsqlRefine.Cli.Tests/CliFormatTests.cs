namespace TsqlRefine.Cli.Tests;

public sealed class CliFormatTests
{
    [Fact]
    public async Task Format_OutputsToStdoutForStdin()
    {
        // Input has CRLF, so Auto mode preserves CRLF
        var stdin = new StringReader("select *\r\n\tfrom t\r\nwhere id=1  ");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = await CliApp.RunAsync(["format", "--stdin"], stdin, stdout, stderr);

        Assert.Equal(0, code);
        // Default identifier casing is None, so identifiers keep original casing
        // CRLF is preserved from input
        Assert.Equal("SELECT *\r\n    FROM t\r\nWHERE id = 1\r\n", stdout.ToString());
        Assert.Equal(string.Empty, stderr.ToString());
    }

    [Fact]
    public async Task Format_WithExplicitLfLineEnding_OutputsLf()
    {
        // Input has CRLF, but explicit --line-ending lf converts to LF
        var stdin = new StringReader("select *\r\n\tfrom t\r\nwhere id=1  ");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = await CliApp.RunAsync(["format", "--stdin", "--line-ending", "lf"], stdin, stdout, stderr);

        Assert.Equal(0, code);
        Assert.Equal("SELECT *\n    FROM t\nWHERE id = 1\n", stdout.ToString());
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
            // Input has no line endings, so Auto mode falls back to CRLF (Windows-preferred)
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
            // Auto mode falls back to CRLF when no line endings in input
            var content = await File.ReadAllTextAsync(sqlPath);
            Assert.Equal("SELECT * FROM t\r\n", content);
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

            // EditorConfig specifies LF line endings
            await File.WriteAllTextAsync(editorConfigPath, "root = true\n[*.sql]\nindent_style = tab\nindent_size = 2\nend_of_line = lf\n");
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

    [Fact]
    public async Task Format_UsesEditorConfigLineEndingForSqlFiles()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"tsqlrefine-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var editorConfigPath = Path.Combine(tempDir, ".editorconfig");
            var sqlPath = Path.Combine(tempDir, "sample.sql");

            // EditorConfig specifies CRLF line endings
            await File.WriteAllTextAsync(editorConfigPath, "root = true\n[*.sql]\nend_of_line = crlf\n");
            // Input has LF, but EditorConfig says CRLF
            await File.WriteAllTextAsync(sqlPath, "select *\n    from t");

            var stdout = new StringWriter();
            var stderr = new StringWriter();

            var code = await CliApp.RunAsync(["format", sqlPath], TextReader.Null, stdout, stderr);

            Assert.Equal(0, code);
            var content = await File.ReadAllTextAsync(sqlPath);
            // EditorConfig specifies CRLF, so output should have CRLF
            Assert.Equal("SELECT *\r\n    FROM t\r\n", content);
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
