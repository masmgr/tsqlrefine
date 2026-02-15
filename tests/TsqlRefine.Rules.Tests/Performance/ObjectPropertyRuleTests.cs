using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Performance;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Performance;

public sealed class ObjectPropertyRuleTests
{
    [Fact]
    public void Metadata_ShouldHaveCorrectProperties()
    {
        var rule = new ObjectPropertyRule();

        Assert.Equal("avoid-objectproperty", rule.Metadata.RuleId);
        Assert.Equal("Performance", rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Warning, rule.Metadata.DefaultSeverity);
        Assert.False(rule.Metadata.Fixable);
    }

    [Theory]
    [InlineData("SELECT OBJECTPROPERTY(OBJECT_ID('dbo.Users'), 'TableHasPrimaryKey')")]
    [InlineData("IF OBJECTPROPERTY(OBJECT_ID('MyTable'), 'IsUserTable') = 1 SELECT 1")]
    [InlineData(@"
        DECLARE @prop INT
        SET @prop = OBJECTPROPERTY(OBJECT_ID('dbo.MyProc'), 'IsProcedure')")]
    public void Analyze_WhenObjectPropertyUsed_ReturnsDiagnostic(string sql)
    {
        var rule = new ObjectPropertyRule();
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("avoid-objectproperty", diagnostics[0].Code);
        Assert.Contains("OBJECTPROPERTY", diagnostics[0].Message);
    }

    [Theory]
    [InlineData("SELECT OBJECTPROPERTYEX(OBJECT_ID('dbo.Users'), 'BaseType')")]
    [InlineData("SELECT * FROM sys.objects WHERE type = 'U'")]
    [InlineData("SELECT OBJECT_ID('dbo.Users')")]
    public void Analyze_WhenNoObjectProperty_ReturnsNoDiagnostic(string sql)
    {
        var rule = new ObjectPropertyRule();
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_WhenMultipleObjectProperty_ReturnsMultipleDiagnostics()
    {
        var sql = @"
            SELECT OBJECTPROPERTY(OBJECT_ID('Table1'), 'IsTable'),
                   OBJECTPROPERTY(OBJECT_ID('Table2'), 'IsView')
        ";

        var rule = new ObjectPropertyRule();
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Equal(2, diagnostics.Length);
        Assert.All(diagnostics, d => Assert.Equal("avoid-objectproperty", d.Code));
    }

    [Fact]
    public void GetFixes_ReturnsNoFixes()
    {
        var rule = new ObjectPropertyRule();
        var context = RuleTestContext.CreateContext("SELECT OBJECTPROPERTY(OBJECT_ID('test'), 'IsTable')");
        var diagnostic = rule.Analyze(context).First();
        var fixes = rule.GetFixes(context, diagnostic).ToArray();

        Assert.Empty(fixes);
    }


}
