using TsqlRefine.Rules.Rules.Correctness;

namespace TsqlRefine.Rules.Tests.Correctness;

public sealed class ExecOutputNotCapturedRuleTests
{
    private const string Definition =
        "CREATE PROCEDURE dbo.TryGet @id int, @found bit OUTPUT AS SELECT @found = 1;";
    private readonly ExecOutputNotCapturedRule _rule = new();

    [Fact]
    public void Analyze_OutputKeywordOmitted_ReturnsDiagnostic()
    {
        var context = ExecCatalogRuleTestHelper.CreateContext(
            "DECLARE @found bit; EXEC dbo.TryGet 1, @found;",
            Definition);

        var diagnostic = Assert.Single(_rule.Analyze(context));

        Assert.Contains("@found", diagnostic.Message);
    }

    [Fact]
    public void Analyze_OutputKeywordSpecified_ReturnsNoDiagnostic()
    {
        var context = ExecCatalogRuleTestHelper.CreateContext(
            "DECLARE @found bit; EXEC dbo.TryGet 1, @found OUTPUT;",
            Definition);

        Assert.Empty(_rule.Analyze(context));
    }
}
