using TsqlRefine.Rules.Rules.Correctness;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Correctness;

public sealed class ExecParameterCountMismatchRuleTests
{
    private const string Definition = """
        CREATE PROCEDURE dbo.SaveUser
            @id int,
            @name nvarchar(50) = N'unknown',
            @active bit
        AS SELECT 1;
        """;
    private readonly ExecParameterCountMismatchRule _rule = new();

    [Fact]
    public void Analyze_MissingRequiredParameter_ReturnsDiagnostic()
    {
        var context = ExecCatalogRuleTestHelper.CreateContext("EXEC dbo.SaveUser 1;", Definition);

        var diagnostic = Assert.Single(_rule.Analyze(context));

        Assert.Equal("exec-parameter-count-mismatch", diagnostic.Code);
        Assert.Contains("@active", diagnostic.Message);
    }

    [Fact]
    public void Analyze_DefaultAndNamedRequiredParameter_ReturnsNoDiagnostic()
    {
        var context = ExecCatalogRuleTestHelper.CreateContext(
            "EXEC dbo.SaveUser 1, @active = 1;",
            Definition);

        Assert.Empty(_rule.Analyze(context));
    }

    [Fact]
    public void Analyze_ExtraPositionalArgument_ReturnsDiagnostic()
    {
        var context = ExecCatalogRuleTestHelper.CreateContext(
            "EXEC dbo.SaveUser 1, N'name', 1, 4;",
            Definition);

        Assert.Single(_rule.Analyze(context));
    }

    [Fact]
    public void Analyze_NoCatalog_ReturnsNoDiagnostic()
    {
        Assert.Empty(_rule.Analyze(RuleTestContext.CreateContext("EXEC dbo.SaveUser;")));
    }
}
