using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Style;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Style;

public sealed class DisallowOrderByOrdinalRuleTests
{
    private readonly DisallowOrderByOrdinalRule _rule = new();

    [Fact]
    public void Analyze_OrderBySingleOrdinal_ReturnsDiagnostic()
    {
        const string sql = "SELECT id, name FROM dbo.Users ORDER BY 1;";
        var context = CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("avoid-order-by-ordinal", diagnostic.Code);
        Assert.Contains("1", diagnostic.Message);
    }

    [Fact]
    public void Analyze_OrderByMultipleOrdinals_ReturnsMultipleDiagnostics()
    {
        const string sql = "SELECT id, name, email FROM dbo.Users ORDER BY 1, 2;";
        var context = CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Equal(2, diagnostics.Length);
        Assert.Contains("1", diagnostics[0].Message);
        Assert.Contains("2", diagnostics[1].Message);
    }

    [Fact]
    public void Analyze_OrderByColumnName_ReturnsNoDiagnostic()
    {
        const string sql = "SELECT id, name FROM dbo.Users ORDER BY name;";
        var context = CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_OrderByMixedOrdinalAndName_ReturnsOneDiagnostic()
    {
        const string sql = "SELECT id, name FROM dbo.Users ORDER BY 1, name;";
        var context = CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        var diagnostic = Assert.Single(diagnostics);
        Assert.Contains("1", diagnostic.Message);
    }

    [Fact]
    public void Analyze_NoOrderBy_ReturnsNoDiagnostic()
    {
        const string sql = "SELECT id, name FROM dbo.Users;";
        var context = CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_TopWithIntegerLiteral_ReturnsNoDiagnostic()
    {
        const string sql = "SELECT TOP 1 id, name FROM dbo.Users;";
        var context = CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_OrderByExpression_ReturnsNoDiagnostic()
    {
        const string sql = "SELECT id, name FROM dbo.Users ORDER BY LEN(name);";
        var context = CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_OrderByOrdinalWithDirection_ReturnsDiagnostic()
    {
        const string sql = "SELECT id, name FROM dbo.Users ORDER BY 1 DESC;";
        var context = CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        var diagnostic = Assert.Single(diagnostics);
        Assert.Contains("1", diagnostic.Message);
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
    public void Metadata_HasCorrectProperties()
    {
        Assert.Equal("avoid-order-by-ordinal", _rule.Metadata.RuleId);
        Assert.Equal("Style", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Information, _rule.Metadata.DefaultSeverity);
        Assert.False(_rule.Metadata.Fixable);
    }

    private static RuleContext CreateContext(string sql)
    {
        return RuleTestContext.CreateContext(sql);
    }
}
