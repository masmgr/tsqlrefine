using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Style;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Style;

public sealed class DuplicateGoRuleTests
{
    [Fact]
    public void Analyze_WhenConsecutiveGo_ReturnsDiagnostic()
    {
        var rule = new DuplicateGoRule();
        var sql = "SELECT 1;\nGO\nGO\nSELECT 2;";
        var context = new RuleContext(
            FilePath: "<test>",
            CompatLevel: 150,
            Ast: new ScriptDomAst(sql),
            Tokens: RuleTestContext.Tokenize(sql),
            Settings: new RuleSettings()
        );

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.NotEmpty(diagnostics);
        Assert.All(diagnostics, d => Assert.Equal("duplicate-go", d.Data?.RuleId));
    }

    [Fact]
    public void Analyze_WhenSingleGo_ReturnsEmpty()
    {
        var rule = new DuplicateGoRule();
        var sql = "SELECT 1;\nGO\nSELECT 2;";
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
    public void Analyze_WhenGoWithEmptyLinesBetween_ReturnsDiagnostic()
    {
        var rule = new DuplicateGoRule();
        var sql = "SELECT 1;\nGO\n\nGO\nSELECT 2;";
        var context = new RuleContext(
            FilePath: "<test>",
            CompatLevel: 150,
            Ast: new ScriptDomAst(sql),
            Tokens: RuleTestContext.Tokenize(sql),
            Settings: new RuleSettings()
        );

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.NotEmpty(diagnostics);
    }

    [Fact]
    public void Analyze_WhenNoGo_ReturnsEmpty()
    {
        var rule = new DuplicateGoRule();
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
    public void Analyze_WhenGoInComment_ReturnsEmpty()
    {
        var rule = new DuplicateGoRule();
        var sql = "-- GO\nSELECT 1;\nGO";
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

}
