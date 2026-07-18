using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Correctness;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Correctness;

public sealed class BanLegacyJoinSyntaxRuleTests
{
    private readonly BanLegacyJoinSyntaxRule _rule = new();

    [Theory]
    [InlineData("SELECT * FROM a, b WHERE a.id *= b.id;", "*=", "LEFT")]
    [InlineData("SELECT * FROM a, b WHERE a.id =* b.id;", "=*", "RIGHT")]
    public void Analyze_LegacyOuterJoin_ReturnsDiagnostic(string sql, string operatorText, string joinType)
    {
        var diagnostics = _rule.Analyze(RuleTestContext.CreateContext(sql)).ToArray();

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("avoid-legacy-join-syntax", diagnostic.Code);
        Assert.Equal("avoid-legacy-join-syntax", diagnostic.Data?.RuleId);
        Assert.Equal("Correctness", diagnostic.Data?.Category);
        Assert.Contains(operatorText, diagnostic.Message);
        Assert.Contains($"{joinType} JOIN", diagnostic.Message);
    }

    [Fact]
    public void Analyze_MultipleLegacyOuterJoins_ReturnsMultipleDiagnostics()
    {
        const string sql = "SELECT * FROM a, b, c WHERE a.id *= b.id AND a.id =* c.id;";

        var diagnostics = _rule.Analyze(RuleTestContext.CreateContext(sql)).ToArray();

        Assert.Equal(2, diagnostics.Length);
    }

    [Theory]
    [InlineData("SET @x *= 2;")]
    [InlineData("SELECT @x *= 2;")]
    [InlineData("UPDATE dbo.Items SET Quantity *= 2;")]
    public void Analyze_LegalMultiplyAssignment_ReturnsEmpty(string sql)
    {
        var diagnostics = _rule.Analyze(RuleTestContext.CreateContext(sql)).ToArray();

        Assert.Empty(diagnostics);
    }

    [Theory]
    [InlineData("SELECT * FROM a LEFT JOIN b ON a.id = b.id;")]
    [InlineData("SELECT * FROM a RIGHT JOIN b ON a.id = b.id;")]
    [InlineData("SELECT price * quantity AS total FROM dbo.Items;")]
    public void Analyze_NonLegacySyntax_ReturnsEmpty(string sql)
    {
        var diagnostics = _rule.Analyze(RuleTestContext.CreateContext(sql)).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_LegacyOuterJoin_ReportsOperatorRange()
    {
        const string sql = "SELECT * FROM a, b WHERE a.id *= b.id;";

        var diagnostic = Assert.Single(_rule.Analyze(RuleTestContext.CreateContext(sql)));

        Assert.Equal(0, diagnostic.Range.Start.Line);
        Assert.Equal(sql.IndexOf("*=", StringComparison.Ordinal), diagnostic.Range.Start.Character);
        Assert.Equal(diagnostic.Range.Start.Character + 2, diagnostic.Range.End.Character);
    }

    [Fact]
    public void GetFixes_ReturnsEmpty()
    {
        var context = RuleTestContext.CreateContext("SELECT * FROM a, b WHERE a.id *= b.id;");
        var diagnostic = Assert.Single(_rule.Analyze(context));

        Assert.Empty(_rule.GetFixes(context, diagnostic));
    }

    [Fact]
    public void Metadata_HasCorrectValues()
    {
        Assert.Equal("avoid-legacy-join-syntax", _rule.Metadata.RuleId);
        Assert.Equal("Correctness", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Error, _rule.Metadata.DefaultSeverity);
        Assert.False(_rule.Metadata.Fixable);
    }
}
