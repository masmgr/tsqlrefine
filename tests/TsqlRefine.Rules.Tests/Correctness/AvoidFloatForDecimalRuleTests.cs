using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Correctness;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Correctness;

public sealed class AvoidFloatForDecimalRuleTests
{
    private readonly AvoidFloatForDecimalRule _rule = new();

    [Fact]
    public void Metadata_HasCorrectProperties()
    {
        Assert.Equal("avoid-float-for-decimal", _rule.Metadata.RuleId);
        Assert.Equal("Correctness", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Warning, _rule.Metadata.DefaultSeverity);
        Assert.False(_rule.Metadata.Fixable);
    }

    [Fact]
    public void Analyze_FloatColumn_ReturnsDiagnostic()
    {
        const string sql = "CREATE TABLE dbo.Products (Price FLOAT NOT NULL);";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("avoid-float-for-decimal", diagnostics[0].Code);
        Assert.Contains("FLOAT", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_RealColumn_ReturnsDiagnostic()
    {
        const string sql = "CREATE TABLE dbo.Products (Price REAL NOT NULL);";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("avoid-float-for-decimal", diagnostics[0].Code);
        Assert.Contains("REAL", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_FloatVariable_ReturnsDiagnostic()
    {
        const string sql = "DECLARE @price FLOAT;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("avoid-float-for-decimal", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_RealVariable_ReturnsDiagnostic()
    {
        const string sql = "DECLARE @price REAL;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("avoid-float-for-decimal", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_FloatParameter_ReturnsDiagnostic()
    {
        const string sql = "CREATE PROCEDURE dbo.UpdatePrice @price FLOAT AS SELECT 1;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("avoid-float-for-decimal", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_FloatWithPrecision_ReturnsDiagnostic()
    {
        const string sql = "CREATE TABLE dbo.Products (Price FLOAT(53) NOT NULL);";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("avoid-float-for-decimal", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_MultipleFloatColumns_ReturnsMultipleDiagnostics()
    {
        const string sql = @"
            CREATE TABLE dbo.Products (
                Price FLOAT NOT NULL,
                Cost REAL NOT NULL,
                Margin FLOAT(24) NOT NULL
            );
        ";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Equal(3, diagnostics.Length);
        Assert.All(diagnostics, d => Assert.Equal("avoid-float-for-decimal", d.Code));
    }

    [Theory]
    [InlineData("CREATE TABLE dbo.Products (Price DECIMAL(18,2) NOT NULL);")]
    [InlineData("CREATE TABLE dbo.Products (Price NUMERIC(10,4) NOT NULL);")]
    [InlineData("CREATE TABLE dbo.Products (Price MONEY NOT NULL);")]
    [InlineData("CREATE TABLE dbo.Products (Price SMALLMONEY NOT NULL);")]
    [InlineData("CREATE TABLE dbo.Products (Id INT NOT NULL);")]
    [InlineData("CREATE TABLE dbo.Products (Name NVARCHAR(100) NOT NULL);")]
    [InlineData("DECLARE @amount DECIMAL(18,2);")]
    [InlineData("")]
    public void Analyze_NonFloatType_NoDiagnostic(string sql)
    {
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void GetFixes_ReturnsEmptyCollection()
    {
        const string sql = "DECLARE @price FLOAT;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostic = _rule.Analyze(context).First();

        var fixes = _rule.GetFixes(context, diagnostic);

        Assert.Empty(fixes);
    }
}
