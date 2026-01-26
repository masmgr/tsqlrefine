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

            var code = await CliApp.RunAsync(new[] { "format", "--detect-encoding", sqlPath }, TextReader.Null, stdout, stderr);

            Assert.Equal(0, code);
            Assert.Contains("-- 日本語", stdout.ToString());
            Assert.Contains("SELECT 1", stdout.ToString());
            Assert.Equal(string.Empty, stderr.ToString());
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
    public async Task Format_Write_PreservesOriginalEncoding()
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

            var expectedStdout = new StringWriter();
            var expectedStderr = new StringWriter();
            var expectedCode = await CliApp.RunAsync(new[] { "format", "--detect-encoding", sqlPath }, TextReader.Null, expectedStdout, expectedStderr);

            Assert.Equal(0, expectedCode);
            Assert.Equal(string.Empty, expectedStderr.ToString());

            var stdout = new StringWriter();
            var stderr = new StringWriter();
            var code = await CliApp.RunAsync(new[] { "format", "--detect-encoding", "--write", sqlPath }, TextReader.Null, stdout, stderr);

            Assert.Equal(0, code);
            Assert.Equal(string.Empty, stdout.ToString());
            Assert.Equal(string.Empty, stderr.ToString());

            var formatted = expectedStdout.ToString();
            var actualBytes = await File.ReadAllBytesAsync(sqlPath);
            Assert.Equal(shiftJis.GetBytes(formatted), actualBytes);
            Assert.NotEqual(Encoding.UTF8.GetBytes(formatted), actualBytes);
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
}
