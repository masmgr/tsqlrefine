using TsqlRefine.Rules.Rules.Performance;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Performance;

public sealed class RedundantSemiJoinRuleTests
{
    private readonly RedundantSemiJoinRule _rule = new();

    [Fact]
    public void Analyze_EquivalentInSubqueryAndInnerJoin_ReturnsDiagnostic()
    {
        const string sql = """
            SELECT v.Id
            FROM dbo.Facts AS v
            INNER JOIN dbo.Base AS b ON b.Id = v.Id
            WHERE v.Id IN (SELECT x.Id FROM dbo.Base AS x);
            """;

        var diagnostic = Assert.Single(_rule.Analyze(RuleTestContext.CreateContext(sql)));

        Assert.Equal("redundant-semi-join", diagnostic.Code);
    }

    [Fact]
    public void Analyze_EquivalentExistsAndInnerJoin_ReturnsDiagnostic()
    {
        const string sql = """
            SELECT v.Id
            FROM dbo.Facts AS v
            INNER JOIN dbo.Base AS b ON v.Id = b.Id
            WHERE EXISTS (SELECT 1 FROM dbo.Base AS x WHERE x.Id = v.Id);
            """;

        Assert.Single(_rule.Analyze(RuleTestContext.CreateContext(sql)));
    }

    [Fact]
    public void Analyze_RepeatedTableIsFirstJoinSide_ReturnsDiagnostic()
    {
        const string sql = "SELECT v.Id FROM dbo.Base AS b INNER JOIN dbo.Facts AS v ON v.Id = b.Id WHERE v.Id IN (SELECT x.Id FROM dbo.Base AS x);";

        Assert.Single(_rule.Analyze(RuleTestContext.CreateContext(sql)));
    }

    [Theory]
    [InlineData("""
        SELECT v.Id FROM dbo.Facts AS v
        LEFT JOIN dbo.Base AS b ON b.Id = v.Id
        WHERE v.Id IN (SELECT x.Id FROM dbo.Base AS x);
        """)]
    [InlineData("""
        SELECT v.Id FROM dbo.Facts AS v
        INNER JOIN dbo.Base AS b ON b.Id = v.Id
        WHERE v.Id NOT IN (SELECT x.Id FROM dbo.Base AS x);
        """)]
    [InlineData("""
        SELECT v.Id FROM dbo.Facts AS v
        INNER JOIN dbo.Base AS b ON b.Id = v.Id
        WHERE v.Id IN (SELECT x.Id FROM dbo.Base AS x WHERE x.Active = 1);
        """)]
    [InlineData("""
        SELECT v.Id FROM dbo.Facts AS v
        INNER JOIN dbo.Base AS b ON b.Id = v.Id
        WHERE v.OtherId IN (SELECT x.Id FROM dbo.Base AS x);
        """)]
    [InlineData("""
        SELECT v.Id FROM dbo.Facts AS v
        INNER JOIN dbo.Base AS b ON b.Id = v.Id
        WHERE EXISTS (SELECT 1 FROM dbo.Base AS x WHERE x.Id = v.Id AND x.Active = 1);
        """)]
    [InlineData("""
        SELECT v.Id FROM dbo.Facts AS v
        INNER JOIN dbo.Base AS b ON b.Id = v.Id
        WHERE v.Id IN (SELECT v.Id FROM dbo.Base AS x);
        """)]
    public void Analyze_NonEquivalentPatterns_ReturnsEmpty(string sql)
    {
        Assert.Empty(_rule.Analyze(RuleTestContext.CreateContext(sql)));
    }

    [Fact]
    public void PreferExistsRule_SuppressesGenericDiagnosticForRedundantIn()
    {
        const string sql = """
            SELECT v.Id
            FROM dbo.Facts AS v
            INNER JOIN dbo.Base AS b ON b.Id = v.Id
            WHERE v.Id IN (SELECT x.Id FROM dbo.Base AS x);
            """;
        var preferExists = new PreferExistsOverInSubqueryRule();

        Assert.Empty(preferExists.Analyze(RuleTestContext.CreateContext(sql)));
    }
}
