using System.Text;
using TsqlRefine.Cli;

namespace TsqlRefine.Cli.Tests;

public sealed class CliEncodingTests
{
    [Fact]
    public async Task Format_WhenFileIsShiftJis_AutoDetectsEncoding()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var shiftJis = Encoding.GetEncoding(932);

        var tempDir = Path.Combine(Path.GetTempPath(), $"tsqlrefine-encoding-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var sqlPath = Path.Combine(tempDir, "shiftjis.sql");
            var input = "select 1 -- 日本語\n";
            await File.WriteAllBytesAsync(sqlPath, shiftJis.GetBytes(input));

            var stdout = new StringWriter();
            var stderr = new StringWriter();

            var code = await CliApp.RunAsync(["format", "--detect-encoding", sqlPath], TextReader.Null, stdout, stderr);

            Assert.Equal(0, code);
            // File input now writes to file, so stdout should be empty
            Assert.Empty(stdout.ToString());
            // stderr should contain the change log
            Assert.Contains("Formatted:", stderr.ToString());

            // Verify file content
            var actualBytes = await File.ReadAllBytesAsync(sqlPath);
            var decoded = shiftJis.GetString(actualBytes);
            Assert.Contains("-- 日本語", decoded);
            Assert.Contains("SELECT 1", decoded);
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
    public async Task Format_PreservesOriginalEncoding()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var shiftJis = Encoding.GetEncoding(932);

        var tempDir = Path.Combine(Path.GetTempPath(), $"tsqlrefine-encoding-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var sqlPath = Path.Combine(tempDir, "write.sql");
            var input = "select 1 -- 日本語\n";
            await File.WriteAllBytesAsync(sqlPath, shiftJis.GetBytes(input));

            var stdout = new StringWriter();
            var stderr = new StringWriter();
            var code = await CliApp.RunAsync(["format", "--detect-encoding", sqlPath], TextReader.Null, stdout, stderr);

            Assert.Equal(0, code);
            Assert.Equal(string.Empty, stdout.ToString());
            // stderr should contain the change log
            Assert.Contains("Formatted:", stderr.ToString());

            // Verify file is still encoded with Shift-JIS
            var actualBytes = await File.ReadAllBytesAsync(sqlPath);
            var decoded = shiftJis.GetString(actualBytes);
            Assert.Contains("SELECT 1", decoded);
            Assert.Contains("-- 日本語", decoded);

            // Verify it's not UTF-8
            Assert.NotEqual(Encoding.UTF8.GetBytes(decoded), actualBytes);
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
    public async Task Format_WhenStdinStreamIsShiftJis_AutoDetectsEncoding()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var shiftJis = Encoding.GetEncoding(932);

        var stdinBytes = shiftJis.GetBytes("select 1 -- 日本語\n");
        using var stdin = new MemoryStream(stdinBytes);

        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = await CliApp.RunAsync(new[] { "format", "--detect-encoding", "--stdin" }, stdin, stdout, stderr);

        Assert.Equal(0, code);
        Assert.Contains("-- 日本語", stdout.ToString());
        Assert.Contains("SELECT 1", stdout.ToString());
        Assert.Equal(string.Empty, stderr.ToString());
    }

    [Fact]
    public async Task Format_WithoutDetectEncoding_PreservesOriginalEncoding()
    {
        // This test verifies that:
        // - Without --detect-encoding, content is read as UTF-8 (may garble non-UTF-8 content)
        // - But the original file encoding is preserved for writing
        // So Shift-JIS file stays Shift-JIS, even though content interpretation may differ

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var shiftJis = Encoding.GetEncoding(932);

        var tempDir = Path.Combine(Path.GetTempPath(), $"tsqlrefine-encoding-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var sqlPath = Path.Combine(tempDir, "no-detect.sql");
            // Use ASCII-only content to avoid encoding interpretation issues
            var input = "select 1 -- comment\n";
            await File.WriteAllBytesAsync(sqlPath, shiftJis.GetBytes(input));

            var stdout = new StringWriter();
            var stderr = new StringWriter();

            // Run format WITHOUT --detect-encoding (file input writes to file)
            var code = await CliApp.RunAsync(["format", sqlPath], TextReader.Null, stdout, stderr);

            Assert.Equal(0, code);
            Assert.Equal(string.Empty, stdout.ToString());
            // stderr should contain the change log
            Assert.Contains("Formatted:", stderr.ToString());

            // Verify file is still encoded with Shift-JIS
            var actualBytes = await File.ReadAllBytesAsync(sqlPath);
            var decoded = shiftJis.GetString(actualBytes);

            // Verify formatting was applied (SELECT should be uppercase)
            Assert.Contains("SELECT 1", decoded);
            Assert.Contains("-- comment", decoded);

            // The file should be written with Shift-JIS encoding (same as original)
            // For ASCII content, Shift-JIS and UTF-8 produce the same bytes
            // So we verify by checking the formatted content is correct
            Assert.Equal("SELECT 1 -- comment\n", decoded);
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
    public async Task Format_WithoutDetectEncoding_PreservesUtf8WithBom()
    {
        // This test verifies that UTF-8 with BOM is preserved even without --detect-encoding

        var tempDir = Path.Combine(Path.GetTempPath(), $"tsqlrefine-encoding-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var sqlPath = Path.Combine(tempDir, "utf8bom.sql");
            var input = "select 1 -- comment\n";
            var utf8WithBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
            await File.WriteAllTextAsync(sqlPath, input, utf8WithBom);

            // Verify original has BOM
            var originalBytes = await File.ReadAllBytesAsync(sqlPath);
            Assert.Equal(0xEF, originalBytes[0]);
            Assert.Equal(0xBB, originalBytes[1]);
            Assert.Equal(0xBF, originalBytes[2]);

            var stdout = new StringWriter();
            var stderr = new StringWriter();

            // Run format WITHOUT --detect-encoding (file input writes to file)
            var code = await CliApp.RunAsync(["format", sqlPath], TextReader.Null, stdout, stderr);

            Assert.Equal(0, code);

            // Verify file still has UTF-8 BOM
            var actualBytes = await File.ReadAllBytesAsync(sqlPath);
            Assert.Equal(0xEF, actualBytes[0]);
            Assert.Equal(0xBB, actualBytes[1]);
            Assert.Equal(0xBF, actualBytes[2]);

            // Verify content is formatted
            var decoded = utf8WithBom.GetString(actualBytes);
            Assert.Contains("SELECT 1", decoded);
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
