using TsqlRefine.Rules.Rules.Correctness;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Correctness;

public sealed class InconsistentResultSetRuleTests
{
    private readonly InconsistentResultSetRule _rule = new();

    [Fact]
    public void Analyze_BranchesReturnDifferentColumns_ReturnsDiagnostic()
    {
        const string sql = """
            CREATE PROCEDURE dbo.GetValue @full bit
            AS
            BEGIN
                IF @full = 1 SELECT Id, Name FROM dbo.Users;
                ELSE SELECT Id FROM dbo.Users;
            END;
            """;

        Assert.Single(_rule.Analyze(RuleTestContext.CreateContext(sql)));
    }

    [Fact]
    public void Analyze_BranchesReturnSameShape_ReturnsNoDiagnostic()
    {
        const string sql = """
            CREATE PROCEDURE dbo.GetValue @full bit
            AS
            BEGIN
                IF @full = 1 SELECT Id, Name FROM dbo.Users;
                ELSE SELECT Id, Name FROM dbo.ArchivedUsers;
            END;
            """;

        Assert.Empty(_rule.Analyze(RuleTestContext.CreateContext(sql)));
    }

    [Fact]
    public void Analyze_SequentialResultSets_ReturnsNoDiagnostic()
    {
        const string sql = "CREATE PROCEDURE dbo.GetValue AS SELECT Id FROM dbo.Users; SELECT Name FROM dbo.Users;";

        Assert.Empty(_rule.Analyze(RuleTestContext.CreateContext(sql)));
    }

    [Fact]
    public void Analyze_SelectAssignment_IsNotAResultSet()
    {
        const string sql = "CREATE PROCEDURE dbo.GetValue AS DECLARE @id int; SELECT @id = 1;";

        Assert.Empty(_rule.Analyze(RuleTestContext.CreateContext(sql)));
    }
}
