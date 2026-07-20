using TsqlRefine.Rules.Rules.Correctness;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Correctness;

public sealed class CursorNotDeallocatedOnPathRuleTests
{
    private const string Declaration = "DECLARE items CURSOR FOR SELECT 1; OPEN items;";
    private readonly CursorNotDeallocatedOnPathRule _rule = new();

    [Fact]
    public void Analyze_OpenWithoutDeallocate_ReturnsDiagnostic()
    {
        Assert.Single(_rule.Analyze(RuleTestContext.CreateContext(Declaration)));
    }

    [Fact]
    public void Analyze_CloseWithoutDeallocate_ReturnsDiagnostic()
    {
        Assert.Single(_rule.Analyze(RuleTestContext.CreateContext($"{Declaration} CLOSE items;")));
    }

    [Fact]
    public void Analyze_DeallocatedOnAllPaths_ReturnsNoDiagnostic()
    {
        const string sql = """
            DECLARE items CURSOR FOR SELECT 1;
            OPEN items;
            IF @done = 1
            BEGIN
                CLOSE items;
                DEALLOCATE items;
            END
            ELSE
            BEGIN
                CLOSE items;
                DEALLOCATE items;
            END;
            """;

        Assert.Empty(_rule.Analyze(RuleTestContext.CreateContext(sql)));
    }

    [Fact]
    public void Analyze_DeallocatedOnOneBranch_ReturnsDiagnostic()
    {
        var sql = $"{Declaration} IF @done = 1 DEALLOCATE items;";

        Assert.Single(_rule.Analyze(RuleTestContext.CreateContext(sql)));
    }
}
