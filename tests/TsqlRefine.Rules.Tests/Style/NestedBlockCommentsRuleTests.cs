using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Style;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Style;

public sealed class NestedBlockCommentsRuleTests
{
    [Fact]
    public void Analyze_WhenNestedBlockComments_ReturnsDiagnostic()
    {
        var rule = new NestedBlockCommentsRule();
        var sql = "/* outer /* inner */ outer */\nSELECT 1;";
        var context = new RuleContext(
            FilePath: "<test>",
            CompatLevel: 150,
            Ast: new ScriptDomAst(sql),
            Tokens: RuleTestContext.Tokenize(sql),
            Settings: new RuleSettings()
        );

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.NotEmpty(diagnostics);
        Assert.All(diagnostics, d => Assert.Equal("nested-block-comments", d.Data?.RuleId));
    }

    [Fact]
    public void Analyze_WhenSimpleBlockComment_ReturnsEmpty()
    {
        var rule = new NestedBlockCommentsRule();
        var sql = "/* simple comment */\nSELECT 1;";
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
    public void Analyze_WhenNoComments_ReturnsEmpty()
    {
        var rule = new NestedBlockCommentsRule();
        var sql = "SELECT 1;";
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
    public void Analyze_WhenLineComment_ReturnsEmpty()
    {
        var rule = new NestedBlockCommentsRule();
        var sql = "-- line comment\nSELECT 1;";
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
    public void Analyze_WhenMultipleBlockComments_ReturnsEmpty()
    {
        var rule = new NestedBlockCommentsRule();
        var sql = "/* comment 1 */ SELECT 1; /* comment 2 */";
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
