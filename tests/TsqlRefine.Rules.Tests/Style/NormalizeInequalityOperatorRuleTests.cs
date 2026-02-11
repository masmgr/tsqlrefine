using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Style;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Style;

public sealed class NormalizeInequalityOperatorRuleTests
{
    private readonly NormalizeInequalityOperatorRule _rule = new();

    [Fact]
    public void Analyze_NotEqualExclamation_ReturnsDiagnostic()
    {
        const string sql = "SELECT * FROM dbo.Users WHERE status != 'active';";
        var context = CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("normalize-inequality-operator", diagnostic.Code);
        Assert.Contains("<>", diagnostic.Message);
        Assert.Contains("!=", diagnostic.Message);
    }

    [Fact]
    public void Analyze_NotEqualBrackets_ReturnsNoDiagnostic()
    {
        const string sql = "SELECT * FROM dbo.Users WHERE status <> 'active';";
        var context = CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_Equals_ReturnsNoDiagnostic()
    {
        const string sql = "SELECT * FROM dbo.Users WHERE status = 'active';";
        var context = CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_MultipleNotEqualExclamation_ReturnsMultipleDiagnostics()
    {
        const string sql = "SELECT * FROM dbo.Users WHERE status != 'active' AND role != 'admin';";
        var context = CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Equal(2, diagnostics.Length);
    }

    [Fact]
    public void Analyze_MixedOperators_ReturnsOneDiagnostic()
    {
        const string sql = "SELECT * FROM dbo.Users WHERE status != 'active' AND role <> 'admin';";
        var context = CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        var diagnostic = Assert.Single(diagnostics);
        Assert.Contains("!=", diagnostic.Message);
    }

    [Fact]
    public void Analyze_EmptyInput_ReturnsNoDiagnostic()
    {
        const string sql = "";
        var context = CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void GetFixes_NotEqualExclamation_ReturnsReplacementFix()
    {
        const string sql = "SELECT * FROM dbo.Users WHERE id != 1;";
        var context = CreateContext(sql);
        var diagnostic = _rule.Analyze(context).Single();

        var fixes = _rule.GetFixes(context, diagnostic).ToArray();

        var fix = Assert.Single(fixes);
        var edit = Assert.Single(fix.Edits);
        Assert.Equal("id <> 1", edit.NewText);
    }

    [Fact]
    public void GetFixes_ComplexExpression_PreservesExpressionText()
    {
        const string sql = "SELECT * FROM dbo.Users WHERE COALESCE(status, 'unknown') != 'active';";
        var context = CreateContext(sql);
        var diagnostic = _rule.Analyze(context).Single();

        var fixes = _rule.GetFixes(context, diagnostic).ToArray();

        var fix = Assert.Single(fixes);
        var edit = Assert.Single(fix.Edits);
        Assert.Equal("COALESCE(status, 'unknown') <> 'active'", edit.NewText);
    }

    [Fact]
    public void GetFixes_FixRangeMatchesDiagnosticRange()
    {
        const string sql = "SELECT * FROM dbo.Users WHERE id != 1;";
        var context = CreateContext(sql);
        var diagnostic = _rule.Analyze(context).Single();

        var fixes = _rule.GetFixes(context, diagnostic).ToArray();

        var fix = Assert.Single(fixes);
        var edit = Assert.Single(fix.Edits);
        Assert.Equal(diagnostic.Range, edit.Range);
    }

    [Fact]
    public void GetFixes_WrongRuleId_ReturnsEmpty()
    {
        const string sql = "SELECT * FROM dbo.Users WHERE id != 1;";
        var context = CreateContext(sql);
        var diagnostic = new Diagnostic(
            Range: new TsqlRefine.PluginSdk.Range(new Position(0, 0), new Position(0, 6)),
            Message: "test",
            Code: "wrong-rule-id"
        );

        var fixes = _rule.GetFixes(context, diagnostic).ToArray();

        Assert.Empty(fixes);
    }

    [Fact]
    public void GetFixes_FixTitle_ContainsExpectedText()
    {
        const string sql = "SELECT * FROM dbo.Users WHERE id != 1;";
        var context = CreateContext(sql);
        var diagnostic = _rule.Analyze(context).Single();

        var fixes = _rule.GetFixes(context, diagnostic).ToArray();

        var fix = Assert.Single(fixes);
        Assert.Contains("<>", fix.Title);
    }

    [Fact]
    public void Metadata_HasCorrectProperties()
    {
        Assert.Equal("normalize-inequality-operator", _rule.Metadata.RuleId);
        Assert.Equal("Style", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Information, _rule.Metadata.DefaultSeverity);
        Assert.True(_rule.Metadata.Fixable);
    }

    private static RuleContext CreateContext(string sql)
    {
        return RuleTestContext.CreateContext(sql);
    }
}
