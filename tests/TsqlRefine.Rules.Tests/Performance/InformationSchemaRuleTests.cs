using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Performance;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Performance;

public sealed class InformationSchemaRuleTests
{
    [Fact]
    public void Metadata_ShouldHaveCorrectProperties()
    {
        var rule = new InformationSchemaRule();

        Assert.Equal("information-schema", rule.Metadata.RuleId);
        Assert.Equal("Performance", rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Information, rule.Metadata.DefaultSeverity);
        Assert.False(rule.Metadata.Fixable);
    }

    [Theory]
    [InlineData("SELECT * FROM INFORMATION_SCHEMA.TABLES")]
    [InlineData("SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Users'")]
    [InlineData("SELECT * FROM INFORMATION_SCHEMA.VIEWS")]
    [InlineData("SELECT COUNT(*) FROM INFORMATION_SCHEMA.ROUTINES")]
    public void Analyze_WhenInformationSchemaUsed_ReturnsDiagnostic(string sql)
    {
        var rule = new InformationSchemaRule();
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("information-schema", diagnostics[0].Code);
        Assert.Contains("INFORMATION_SCHEMA", diagnostics[0].Message);
    }

    [Theory]
    [InlineData("SELECT * FROM sys.tables")]
    [InlineData("SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Users')")]
    [InlineData("SELECT * FROM dbo.Users")]
    [InlineData("SELECT * FROM master.dbo.sysprocesses")]
    public void Analyze_WhenNoInformationSchema_ReturnsNoDiagnostic(string sql)
    {
        var rule = new InformationSchemaRule();
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_WhenMultipleInformationSchemaReferences_ReturnsMultipleDiagnostics()
    {
        var sql = @"
            SELECT * FROM INFORMATION_SCHEMA.TABLES
            UNION ALL
            SELECT * FROM INFORMATION_SCHEMA.VIEWS
        ";

        var rule = new InformationSchemaRule();
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Equal(2, diagnostics.Length);
        Assert.All(diagnostics, d => Assert.Equal("information-schema", d.Code));
    }

    [Fact]
    public void GetFixes_ReturnsNoFixes()
    {
        var rule = new InformationSchemaRule();
        var context = RuleTestContext.CreateContext("SELECT * FROM INFORMATION_SCHEMA.TABLES");
        var diagnostic = rule.Analyze(context).First();
        var fixes = rule.GetFixes(context, diagnostic).ToArray();

        Assert.Empty(fixes);
    }


}
