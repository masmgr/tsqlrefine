namespace TsqlRefine.Cli.Tests;

public sealed class CliUtf8Tests
{
    [Fact]
    public void Parse_WithUtf8Flag_SetsUtf8True()
    {
        var args = CliParser.Parse(["lint", "--stdin", "--utf8"]);

        Assert.True(args.Utf8);
    }

    [Fact]
    public void Parse_WithoutUtf8Flag_SetsUtf8False()
    {
        var args = CliParser.Parse(["lint", "--stdin"]);

        Assert.False(args.Utf8);
    }

    [Fact]
    public void Parse_Utf8WithFormatCommand_SetsUtf8True()
    {
        var args = CliParser.Parse(["format", "--stdin", "--utf8"]);

        Assert.True(args.Utf8);
    }

    [Fact]
    public void Parse_Utf8WithFixCommand_SetsUtf8True()
    {
        var args = CliParser.Parse(["fix", "--stdin", "--utf8"]);

        Assert.True(args.Utf8);
    }

    [Fact]
    public void Parse_Utf8WithDefaultCommand_SetsUtf8True()
    {
        var args = CliParser.Parse(["--stdin", "--utf8"]);

        Assert.True(args.Utf8);
    }

    [Fact]
    public async Task Lint_WithUtf8Flag_AcceptedAsValidOption()
    {
        var stdin = new StringReader("SELECT id FROM dbo.t;");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = await CliApp.RunAsync(
            ["lint", "--stdin", "--utf8"], stdin, stdout, stderr);

        Assert.Equal(0, code);
    }

    [Fact]
    public async Task Format_WithUtf8Flag_AcceptedAsValidOption()
    {
        var stdin = new StringReader("select 1;");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = await CliApp.RunAsync(
            ["format", "--stdin", "--utf8"], stdin, stdout, stderr);

        Assert.Equal(0, code);
        Assert.Contains("SELECT 1", stdout.ToString());
    }
}
