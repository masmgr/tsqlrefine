using TsqlRefine.Rules.Rules.Schema;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Schema;

public sealed class RequireNamedDefaultConstraintRuleTests
{
    private readonly RequireNamedDefaultConstraintRule _rule = new();

    [Fact]
    public void Analyze_UnnamedDefaultOnPermanentTable_ReturnsDiagnostic() =>
        Assert.Single(_rule.Analyze(RuleTestContext.CreateContext("CREATE TABLE dbo.T (ID int DEFAULT ((0)));")));

    [Theory]
    [InlineData("CREATE TABLE dbo.T (ID int CONSTRAINT DF_T_ID DEFAULT ((0)));")]
    [InlineData("CREATE TABLE #T (ID int DEFAULT ((0)));")]
    [InlineData("DECLARE @T TABLE (ID int DEFAULT ((0)));")]
    public void Analyze_NamedOrLocalDefault_ReturnsEmpty(string sql) =>
        Assert.Empty(_rule.Analyze(RuleTestContext.CreateContext(sql)));

    [Fact]
    public void Analyze_AlterTableAddUnnamedDefault_ReturnsDiagnostic() =>
        Assert.Single(_rule.Analyze(RuleTestContext.CreateContext(
            "ALTER TABLE dbo.T ADD IS_VALID tinyint DEFAULT ((1));")));
}
