using TsqlRefine.Rules.Rules.Correctness;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Correctness;

public sealed class UnreachableStatementRuleTests
{
    private readonly UnreachableStatementRule _rule = new();

    [Theory]
    [InlineData("RETURN; SELECT 1;")]
    [InlineData("THROW 50000, 'failed', 1; SELECT 1;")]
    [InlineData("IF 1 = 1 RETURN; SELECT 1;")]
    public void Analyze_UnreachableStatement_ReturnsDiagnostic(string sql)
    {
        Assert.Single(_rule.Analyze(RuleTestContext.CreateContext(sql)));
    }

    [Fact]
    public void Analyze_ReachableStatements_ReturnsNoDiagnostic()
    {
        const string sql = "IF @flag = 1 SELECT 1; ELSE SELECT 2; SELECT 3;";

        Assert.Empty(_rule.Analyze(RuleTestContext.CreateContext(sql)));
    }

    [Theory]
    [InlineData("IF 1 = 0 SELECT 1;")]
    [InlineData("IF 'a' = 'A' SELECT 1;")]
    [InlineData("IF 'a' = 'a ' SELECT 1;")]
    [InlineData("IF 1 = 01 SELECT 1;")]
    [InlineData("IF 1 = 1.0 SELECT 1;")]
    public void Analyze_LiteralComparisonThatIsNotExactlyEqual_DoesNotPruneBranch(string sql)
    {
        Assert.Empty(_rule.Analyze(RuleTestContext.CreateContext(sql)));
    }
}
