using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Performance;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Performance;

public sealed class AvoidScalarUdfInQueryRuleTests
{
    private readonly AvoidScalarUdfInQueryRule _rule = new();

    [Fact]
    public void Metadata_HasCorrectProperties()
    {
        Assert.Equal("avoid-scalar-udf-in-query", _rule.Metadata.RuleId);
        Assert.Equal("Performance", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Warning, _rule.Metadata.DefaultSeverity);
        Assert.False(_rule.Metadata.Fixable);
    }

    [Fact]
    public void Analyze_ScalarUdfInSelect_ReturnsDiagnostic()
    {
        const string sql = "SELECT dbo.MyFunc(col) FROM t;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("avoid-scalar-udf-in-query", diagnostics[0].Code);
        Assert.Contains("dbo.MyFunc", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_ScalarUdfInWhere_ReturnsDiagnostic()
    {
        const string sql = "SELECT * FROM t WHERE dbo.MyFunc(col) = 1;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("avoid-scalar-udf-in-query", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_ScalarUdfInJoinOn_ReturnsDiagnostic()
    {
        const string sql = "SELECT * FROM t1 JOIN t2 ON dbo.Fn(t1.col) = t2.col;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
    }

    [Fact]
    public void Analyze_ScalarUdfInHaving_ReturnsDiagnostic()
    {
        const string sql = "SELECT col, COUNT(*) FROM t GROUP BY col HAVING dbo.MyFunc(col) > 0;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
    }

    [Fact]
    public void Analyze_MultipleUdfs_ReturnsMultipleDiagnostics()
    {
        const string sql = "SELECT dbo.Fn1(a), dbo.Fn2(b) FROM t;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Equal(2, diagnostics.Length);
    }

    [Fact]
    public void Analyze_SysSchemaUdf_ReturnsDiagnostic()
    {
        const string sql = "SELECT sys.fn_listextendedproperty(NULL, NULL, NULL, NULL, NULL, NULL, NULL);";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Contains("sys.fn_listextendedproperty", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_UdfOnLiteral_ReturnsDiagnostic()
    {
        const string sql = "SELECT dbo.MyFunc(1) FROM t;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
    }

    [Theory]
    [InlineData("SELECT GETDATE();")]
    [InlineData("SELECT UPPER(col) FROM t;")]
    [InlineData("SELECT ISNULL(col, 0) FROM t;")]
    [InlineData("SELECT COUNT(*) FROM t;")]
    [InlineData("SELECT CAST(col AS INT) FROM t;")]
    [InlineData("SELECT * FROM t;")]
    public void Analyze_BuiltInFunctions_NoDiagnostic(string sql)
    {
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_TableValuedFunction_NoDiagnostic()
    {
        const string sql = "SELECT * FROM dbo.MyTableValuedFunc(1);";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_ScalarUdfInDeclareAssignment_NoDiagnostic()
    {
        const string sql = "DECLARE @x INT = dbo.MyFunc(1);";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_ScalarUdfInSetAssignment_NoDiagnostic()
    {
        const string sql = "DECLARE @x INT; SET @x = dbo.MyFunc(1);";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void GetFixes_ReturnsEmptyCollection()
    {
        const string sql = "SELECT dbo.MyFunc(col) FROM t;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostic = _rule.Analyze(context).First();
        var fixes = _rule.GetFixes(context, diagnostic);

        Assert.Empty(fixes);
    }
}
