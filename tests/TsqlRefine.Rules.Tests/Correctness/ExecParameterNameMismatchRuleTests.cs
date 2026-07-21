using TsqlRefine.Rules.Rules.Correctness;

namespace TsqlRefine.Rules.Tests.Correctness;

public sealed class ExecParameterNameMismatchRuleTests
{
    private const string Definition = "CREATE PROCEDURE dbo.FindUser @id int AS SELECT @id;";
    private readonly ExecParameterNameMismatchRule _rule = new();

    [Fact]
    public void Analyze_UnknownNamedParameter_ReturnsDiagnostic()
    {
        var context = ExecCatalogRuleTestHelper.CreateContext("EXEC dbo.FindUser @userId = 1;", Definition);

        var diagnostic = Assert.Single(_rule.Analyze(context));

        Assert.Contains("@userId", diagnostic.Message);
    }

    [Fact]
    public void Analyze_ExistingNamedParameter_IgnoresCaseAndReturnsNoDiagnostic()
    {
        var context = ExecCatalogRuleTestHelper.CreateContext("EXEC dbo.FindUser @ID = 1;", Definition);

        Assert.Empty(_rule.Analyze(context));
    }

    [Fact]
    public void Analyze_DuplicateNamedParameter_ReturnsDiagnostic()
    {
        var context = ExecCatalogRuleTestHelper.CreateContext(
            "EXEC dbo.FindUser @id = 1, @ID = 2;",
            Definition);

        var diagnostic = Assert.Single(_rule.Analyze(context));

        Assert.Contains("specified more than once", diagnostic.Message);
    }
}
