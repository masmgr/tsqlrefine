using TsqlRefine.Rules.Rules.Correctness;

namespace TsqlRefine.Rules.Tests.Correctness;

public sealed class ExecInvalidOutputArgumentRuleTests
{
    private const string Definition =
        "CREATE PROCEDURE dbo.TryGet @id int, @found bit OUTPUT AS SELECT @found = 1;";
    private readonly ExecInvalidOutputArgumentRule _rule = new();

    [Fact]
    public void Analyze_OutputSpecifiedForInputParameter_ReturnsDiagnostic()
    {
        var context = ExecCatalogRuleTestHelper.CreateContext(
            "DECLARE @id int; EXEC dbo.TryGet @id OUTPUT, @found = @id OUTPUT;",
            Definition);

        var diagnostic = Assert.Single(_rule.Analyze(context));

        Assert.Contains("not declared as OUTPUT", diagnostic.Message);
    }

    [Fact]
    public void Analyze_OutputVariableForOutputParameter_ReturnsNoDiagnostic()
    {
        var context = ExecCatalogRuleTestHelper.CreateContext(
            "DECLARE @found bit; EXEC dbo.TryGet 1, @found OUTPUT;",
            Definition);

        Assert.Empty(_rule.Analyze(context));
    }
}
