using TsqlRefine.Rules.Rules.Correctness;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Correctness;

public sealed class LenForEmptinessCheckRuleTests
{
    private readonly LenForEmptinessCheckRule _rule = new();

    [Theory]
    [InlineData("SELECT * FROM dbo.Products WHERE LEN(Name) = 0;")]
    [InlineData("SELECT * FROM dbo.Products WHERE LEN(Name) > 0;")]
    [InlineData("SELECT * FROM dbo.Products WHERE LEN(Name) <= 0;")]
    [InlineData("SELECT * FROM dbo.Products WHERE LEN(Name) <> 0;")]
    [InlineData("SELECT * FROM dbo.Products WHERE 0 = LEN(Name);")]
    [InlineData("SELECT * FROM dbo.Products WHERE 0 < LEN(Name);")]
    public void Analyze_LenComparedToZero_ReturnsDiagnostic(string sql)
    {
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("len-for-emptiness-check", diagnostic.Code);
    }

    [Theory]
    [InlineData("SELECT * FROM dbo.Products WHERE DATALENGTH(Name) = 0;")]
    [InlineData("SELECT * FROM dbo.Products WHERE LEN(Name) = LEN(Code);")]
    [InlineData("SELECT * FROM dbo.Products WHERE LEN(Code) < 5;")]
    [InlineData("SELECT * FROM dbo.Products WHERE LEN(Code) <= 5;")]
    [InlineData("SELECT * FROM dbo.Products WHERE Name = '';")]
    [InlineData("SELECT LEN(Name) FROM dbo.Products;")]
    public void Analyze_NonViolatingPatterns_ReturnsEmpty(string sql)
    {
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_LenLowercase_ReturnsDiagnostic()
    {
        const string sql = "SELECT * FROM dbo.Products WHERE len(Name) = 0;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
    }
}
