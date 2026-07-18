using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Performance;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Performance;

public sealed class DisallowSelectDistinctRuleTests
{
    private readonly DisallowSelectDistinctRule _rule = new();

    [Theory]
    [InlineData("SELECT DISTINCT id FROM dbo.Items;")]
    [InlineData("select distinct id from dbo.Items;")]
    [InlineData("SELECT * FROM (SELECT DISTINCT id FROM dbo.Items) AS i;")]
    public void Analyze_SelectDistinct_ReturnsDiagnostic(string sql)
    {
        var diagnostics = _rule.Analyze(RuleTestContext.CreateContext(sql)).ToArray();

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("avoid-select-distinct", diagnostic.Code);
        Assert.Equal("avoid-select-distinct", diagnostic.Data?.RuleId);
        Assert.Equal("Performance", diagnostic.Data?.Category);
        Assert.Contains("DISTINCT", diagnostic.Message);
    }

    [Fact]
    public void Analyze_MultipleSelectDistinct_ReturnsMultipleDiagnostics()
    {
        const string sql = "SELECT DISTINCT id FROM dbo.Items; SELECT DISTINCT name FROM dbo.Users;";

        var diagnostics = _rule.Analyze(RuleTestContext.CreateContext(sql)).ToArray();

        Assert.Equal(2, diagnostics.Length);
    }

    [Theory]
    [InlineData("SELECT id FROM dbo.Items;")]
    [InlineData("SELECT ALL id FROM dbo.Items;")]
    [InlineData("SELECT COUNT(DISTINCT id) FROM dbo.Items;")]
    [InlineData("SELECT id FROM dbo.Items UNION SELECT id FROM dbo.ArchivedItems;")]
    public void Analyze_NonSelectDistinctSyntax_ReturnsEmpty(string sql)
    {
        var diagnostics = _rule.Analyze(RuleTestContext.CreateContext(sql)).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_SelectDistinct_ReportsDistinctKeywordRange()
    {
        const string sql = "SELECT DISTINCT id FROM dbo.Items;";

        var diagnostic = Assert.Single(_rule.Analyze(RuleTestContext.CreateContext(sql)));

        Assert.Equal(0, diagnostic.Range.Start.Line);
        Assert.Equal(7, diagnostic.Range.Start.Character);
        Assert.Equal(15, diagnostic.Range.End.Character);
    }

    [Fact]
    public void GetFixes_ReturnsEmpty()
    {
        var context = RuleTestContext.CreateContext("SELECT DISTINCT id FROM dbo.Items;");
        var diagnostic = Assert.Single(_rule.Analyze(context));

        Assert.Empty(_rule.GetFixes(context, diagnostic));
    }

    [Fact]
    public void Metadata_HasCorrectValues()
    {
        Assert.Equal("avoid-select-distinct", _rule.Metadata.RuleId);
        Assert.Equal("Performance", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Information, _rule.Metadata.DefaultSeverity);
        Assert.False(_rule.Metadata.Fixable);
    }
}
