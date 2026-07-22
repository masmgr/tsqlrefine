namespace TsqlRefine.Cli.Tests;

public sealed class CliRuleOptionTests
{
    [Fact]
    public void Parse_WithRuleOption_ExtractsRuleId()
    {
        var args = CliParser.Parse(new[] { "fix", "--rule", "avoid-select-star", "file.sql" });

        Assert.Equal("avoid-select-star", args.RuleId);
    }

    [Fact]
    public void Parse_LintWithRuleOption_ExtractsRuleId()
    {
        var args = CliParser.Parse(new[] { "lint", "--rule", "avoid-select-star", "file.sql" });

        Assert.Equal("avoid-select-star", args.RuleId);
    }

    [Fact]
    public void Parse_WithoutRuleOption_RuleIdIsNull()
    {
        var args = CliParser.Parse(new[] { "fix", "file.sql" });

        Assert.Null(args.RuleId);
    }

    [Fact]
    public void Parse_RuleOptionCasePreserved()
    {
        var args = CliParser.Parse(new[] { "fix", "--rule", "Avoid-Select-Star", "file.sql" });

        Assert.Equal("Avoid-Select-Star", args.RuleId);
    }

    [Fact]
    public async Task Fix_WithUnknownRuleId_ReturnsConfigError()
    {
        var stdin = new StringReader("SELECT * FROM users;");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = await CliApp.RunAsync(
            new[] { "fix", "--stdin", "--rule", "nonexistent-rule-id" },
            stdin, stdout, stderr);

        Assert.Equal(ExitCodes.ConfigError, code);
        Assert.Contains("Unknown rule ID", stderr.ToString());
    }

    [Fact]
    public async Task Lint_WithUnknownRuleId_ReturnsConfigError()
    {
        var stdin = new StringReader("SELECT * FROM users;");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = await CliApp.RunAsync(
            new[] { "lint", "--stdin", "--rule", "nonexistent-rule-id" },
            stdin, stdout, stderr);

        Assert.Equal(ExitCodes.ConfigError, code);
        Assert.Contains("Unknown rule ID", stderr.ToString());
    }

    [Fact]
    public async Task Lint_WithRuleOption_RunsOnlySelectedRule()
    {
        var stdin = new StringReader("SELECT * FROM users");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = await CliApp.RunAsync(
            new[] { "lint", "--stdin", "--rule", "avoid-select-star", "--output", "json", "--quiet" },
            stdin, stdout, stderr);

        Assert.Equal(ExitCodes.Violations, code);
        Assert.Contains("avoid-select-star", stdout.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("semicolon-termination", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Fix_WithNonFixableRule_ReturnsConfigError()
    {
        // avoid-select-star is not fixable.
        var stdin = new StringReader("SELECT * FROM users;");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = await CliApp.RunAsync(
            new[] { "fix", "--stdin", "--rule", "avoid-select-star" },
            stdin, stdout, stderr);

        Assert.Equal(ExitCodes.ConfigError, code);
        Assert.Contains("does not support auto-fix", stderr.ToString());
    }
}
