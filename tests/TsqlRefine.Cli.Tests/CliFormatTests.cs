using TsqlRefine.Cli;

namespace TsqlRefine.Cli.Tests;

public sealed class CliFormatTests
{
    [Fact]
    public async Task Format_WhenRun_UppercasesKeywordsAndNormalizesWhitespace()
    {
        var stdin = new StringReader("select *\r\n\tfrom t\r\nwhere id=1  ");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = await CliApp.RunAsync(new[] { "format", "--stdin" }, stdin, stdout, stderr);

        Assert.Equal(0, code);
        Assert.Equal("SELECT *\n    FROM t\nWHERE id=1", stdout.ToString());
        Assert.Equal(string.Empty, stderr.ToString());
    }

    [Fact]
    public async Task Format_DoesNotChangeStringsOrComments()
    {
        var stdin = new StringReader("select '--select' as s -- select\nselect [from] from t;\n");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = await CliApp.RunAsync(new[] { "format", "--stdin" }, stdin, stdout, stderr);

        Assert.Equal(0, code);
        Assert.Equal("SELECT '--select' AS s -- select\nSELECT [from] FROM t;\n", stdout.ToString());
        Assert.Equal(string.Empty, stderr.ToString());
    }
}
