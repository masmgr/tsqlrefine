using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Style;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Style;

public sealed class PreferUnicodeStringLiteralsRuleTests
{
    private readonly PreferUnicodeStringLiteralsRule _rule = new();

    [Fact]
    public void Analyze_SelectLiteral_ReturnsDiagnostic()
    {
        const string sql = "SELECT 'Hello';";
        var context = CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("prefer-unicode-string-literals", diagnostics[0].Code);
        Assert.True(diagnostics[0].Data?.Fixable);
    }

    [Fact]
    public void Analyze_UnicodeLiteral_ReturnsNoDiagnostic()
    {
        const string sql = "SELECT N'Hello';";
        var context = CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_VarcharDeclaration_ReturnsNoDiagnostic()
    {
        const string sql = "DECLARE @v VARCHAR(10) = 'abc';";
        var context = CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_CastToVarchar_ReturnsNoDiagnostic()
    {
        const string sql = "SELECT CAST('abc' AS VARCHAR(10));";
        var context = CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_FunctionCall_ReturnsNoDiagnostic()
    {
        const string sql = "SELECT HASHBYTES('SHA2_256', 'abc');";
        var context = CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void GetFixes_AddsUnicodePrefix()
    {
        const string sql = "SELECT 'Hello';";
        var context = CreateContext(sql);
        var diagnostic = _rule.Analyze(context).Single();

        var fixes = _rule.GetFixes(context, diagnostic).ToArray();

        Assert.Single(fixes);
        Assert.Equal("Prefix string literal with N", fixes[0].Title);
        Assert.Single(fixes[0].Edits);
        Assert.Equal("N", fixes[0].Edits[0].NewText);
    }

    [Fact]
    public void Metadata_HasCorrectProperties()
    {
        Assert.Equal("prefer-unicode-string-literals", _rule.Metadata.RuleId);
        Assert.Equal("Style", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Information, _rule.Metadata.DefaultSeverity);
        Assert.True(_rule.Metadata.Fixable);
    }

    private static RuleContext CreateContext(string sql)
    {
        return RuleTestContext.CreateContext(sql);
    }
}
