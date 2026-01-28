using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Style;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Style;

public sealed class DuplicateEmptyLineRuleTests
{
    [Fact]
    public void Analyze_WhenDuplicateEmptyLines_ReturnsDiagnostic()
    {
        var rule = new DuplicateEmptyLineRule();
        var sql = "SELECT 1;\n\n\nSELECT 2;";
        var context = new RuleContext(
            FilePath: "<test>",
            CompatLevel: 150,
            Ast: new ScriptDomAst(sql),
            Tokens: RuleTestContext.Tokenize(sql),
            Settings: new RuleSettings()
        );

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.NotEmpty(diagnostics);
        Assert.All(diagnostics, d => Assert.Equal("duplicate-empty-line", d.Data?.RuleId));
    }

    [Fact]
    public void Analyze_WhenSingleEmptyLine_ReturnsEmpty()
    {
        var rule = new DuplicateEmptyLineRule();
        var sql = "SELECT 1;\n\nSELECT 2;";
        var context = new RuleContext(
            FilePath: "<test>",
            CompatLevel: 150,
            Ast: new ScriptDomAst(sql),
            Tokens: RuleTestContext.Tokenize(sql),
            Settings: new RuleSettings()
        );

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_WhenNoEmptyLines_ReturnsEmpty()
    {
        var rule = new DuplicateEmptyLineRule();
        var sql = "SELECT 1;\nSELECT 2;";
        var context = new RuleContext(
            FilePath: "<test>",
            CompatLevel: 150,
            Ast: new ScriptDomAst(sql),
            Tokens: RuleTestContext.Tokenize(sql),
            Settings: new RuleSettings()
        );

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_WhenMultipleDuplicateEmptyLines_ReturnsMultipleDiagnostics()
    {
        var rule = new DuplicateEmptyLineRule();
        var sql = "SELECT 1;\n\n\nSELECT 2;\n\n\n\nSELECT 3;";
        var context = new RuleContext(
            FilePath: "<test>",
            CompatLevel: 150,
            Ast: new ScriptDomAst(sql),
            Tokens: RuleTestContext.Tokenize(sql),
            Settings: new RuleSettings()
        );

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.NotEmpty(diagnostics);
        Assert.True(diagnostics.Length >= 2);
    }

}
