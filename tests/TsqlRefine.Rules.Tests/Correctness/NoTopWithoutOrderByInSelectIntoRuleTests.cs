using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Correctness;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Correctness;

public sealed class NoTopWithoutOrderByInSelectIntoRuleTests
{
    private readonly NoTopWithoutOrderByInSelectIntoRule _rule = new();

    [Theory]
    [InlineData("SELECT TOP 10 id INTO dbo.TopItems FROM dbo.Items;")]
    [InlineData("SELECT TOP (10) id INTO #TopItems FROM dbo.Items;")]
    [InlineData("SELECT TOP 10 PERCENT id INTO #TopItems FROM dbo.Items;")]
    [InlineData("SELECT TOP (@percent) PERCENT id INTO #TopItems FROM dbo.Items;")]
    public void Analyze_TopSelectIntoWithoutOrderBy_ReturnsDiagnostic(string sql)
    {
        var diagnostics = _rule.Analyze(RuleTestContext.CreateContext(sql)).ToArray();

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("avoid-top-without-order-by-in-select-into", diagnostic.Code);
        Assert.Equal("avoid-top-without-order-by-in-select-into", diagnostic.Data?.RuleId);
        Assert.Equal("Correctness", diagnostic.Data?.Category);
    }

    [Theory]
    [InlineData("SELECT TOP 10 id INTO #TopItems FROM dbo.Items ORDER BY id;")]
    [InlineData("SELECT id INTO #Items FROM dbo.Items;")]
    [InlineData("SELECT TOP 10 id FROM dbo.Items;")]
    [InlineData("SELECT TOP 0 id INTO #Items FROM dbo.Items;")]
    [InlineData("SELECT TOP (0) id INTO #Items FROM dbo.Items;")]
    [InlineData("SELECT TOP (0.00) id INTO #Items FROM dbo.Items;")]
    [InlineData("SELECT TOP 0 PERCENT id INTO #Items FROM dbo.Items;")]
    public void Analyze_NonViolatingSelect_ReturnsEmpty(string sql)
    {
        var diagnostics = _rule.Analyze(RuleTestContext.CreateContext(sql)).ToArray();

        Assert.Empty(diagnostics);
    }

    [Theory]
    [InlineData("SELECT TOP 100 PERCENT id INTO #Items FROM dbo.Items;")]
    [InlineData("SELECT TOP (100) PERCENT id INTO #Items FROM dbo.Items;")]
    [InlineData("SELECT TOP (100.00) PERCENT id INTO #Items FROM dbo.Items;")]
    public void Analyze_TopOneHundredPercent_ReturnsEmpty(string sql)
    {
        var diagnostics = _rule.Analyze(RuleTestContext.CreateContext(sql)).ToArray();

        Assert.Empty(diagnostics);
    }

    [Theory]
    [InlineData("SELECT TOP 1 id INTO #Items FROM dbo.Items UNION ALL SELECT id FROM dbo.ArchivedItems;")]
    [InlineData("SELECT id INTO #Items FROM dbo.Items UNION ALL SELECT TOP 1 id FROM dbo.ArchivedItems;")]
    public void Analyze_UnionBranchWithTopWithoutOrderBy_ReturnsDiagnostic(string sql)
    {
        var diagnostics = _rule.Analyze(RuleTestContext.CreateContext(sql)).ToArray();

        Assert.Single(diagnostics);
    }

    [Fact]
    public void Analyze_MultipleUnionBranchesWithTop_ReturnsMultipleDiagnostics()
    {
        const string sql = "SELECT TOP 1 id INTO #Items FROM dbo.Items UNION ALL SELECT TOP 2 id FROM dbo.ArchivedItems;";

        var diagnostics = _rule.Analyze(RuleTestContext.CreateContext(sql)).ToArray();

        Assert.Equal(2, diagnostics.Length);
    }

    [Fact]
    public void Analyze_TopSelectInto_ReportsTopClauseRange()
    {
        const string sql = "SELECT TOP (10) id INTO #TopItems FROM dbo.Items;";

        var diagnostic = Assert.Single(_rule.Analyze(RuleTestContext.CreateContext(sql)));

        Assert.Equal(0, diagnostic.Range.Start.Line);
        Assert.Equal(7, diagnostic.Range.Start.Character);
        Assert.Equal(15, diagnostic.Range.End.Character);
    }

    [Fact]
    public void Analyze_TopSelectInto_MessageDoesNotCallTargetPermanentTable()
    {
        const string sql = "SELECT TOP (10) id INTO #TopItems FROM dbo.Items;";

        var diagnostic = Assert.Single(_rule.Analyze(RuleTestContext.CreateContext(sql)));

        Assert.DoesNotContain("permanent table", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("non-deterministic rows", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetFixes_ReturnsEmpty()
    {
        var context = RuleTestContext.CreateContext("SELECT TOP 10 id INTO #TopItems FROM dbo.Items;");
        var diagnostic = Assert.Single(_rule.Analyze(context));

        Assert.Empty(_rule.GetFixes(context, diagnostic));
    }

    [Fact]
    public void Metadata_HasCorrectValues()
    {
        Assert.Equal("avoid-top-without-order-by-in-select-into", _rule.Metadata.RuleId);
        Assert.Equal("Correctness", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Warning, _rule.Metadata.DefaultSeverity);
        Assert.False(_rule.Metadata.Fixable);
    }
}
