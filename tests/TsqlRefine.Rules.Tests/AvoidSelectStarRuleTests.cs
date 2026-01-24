using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules;

namespace TsqlRefine.Rules.Tests;

public sealed class AvoidSelectStarRuleTests
{
    [Fact]
    public void Analyze_WhenSelectStar_ReturnsDiagnostic()
    {
        var rule = new AvoidSelectStarRule();
        var context = new RuleContext(
            FilePath: "<test>",
            CompatLevel: 150,
            Ast: new ScriptDomAst("select * from t;"),
            Tokens: Array.Empty<Token>(),
            Settings: new RuleSettings()
        );

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("avoid-select-star", diagnostics[0].Data?.RuleId);
    }

    [Fact]
    public void Analyze_WhenNoSelectStar_ReturnsEmpty()
    {
        var rule = new AvoidSelectStarRule();
        var context = new RuleContext(
            FilePath: "<test>",
            CompatLevel: 150,
            Ast: new ScriptDomAst("select id from t;"),
            Tokens: Array.Empty<Token>(),
            Settings: new RuleSettings()
        );

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }
}

