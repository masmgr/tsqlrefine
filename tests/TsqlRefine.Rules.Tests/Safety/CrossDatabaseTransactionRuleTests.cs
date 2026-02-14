using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Safety;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Safety;

public sealed class CrossDatabaseTransactionRuleTests
{
    [Fact]
    public void Metadata_ShouldHaveCorrectProperties()
    {
        var rule = new CrossDatabaseTransactionRule();

        Assert.Equal("cross-database-transaction", rule.Metadata.RuleId);
        Assert.Equal("Safety", rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Warning, rule.Metadata.DefaultSeverity);
        Assert.False(rule.Metadata.Fixable);
    }

    [Theory]
    [InlineData(@"
        BEGIN TRANSACTION
        INSERT INTO DB1.dbo.Table1 VALUES (1)
        INSERT INTO DB2.dbo.Table2 VALUES (2)
        COMMIT")]
    [InlineData(@"
        BEGIN TRAN
        UPDATE Database1.dbo.Users SET Name = 'Test'
        UPDATE Database2.dbo.Orders SET Status = 'Done'
        COMMIT")]
    [InlineData(@"
        BEGIN TRANSACTION
        DELETE FROM [DB1].[dbo].[Table1]
        DELETE FROM [DB2].[dbo].[Table2]
        ROLLBACK")]
    public void Analyze_WhenCrossDatabaseTransaction_ReturnsDiagnostic(string sql)
    {
        var rule = new CrossDatabaseTransactionRule();
        var context = CreateContext(sql);
        var diagnostics = rule.Analyze(context).ToArray();

        Assert.NotEmpty(diagnostics);
        Assert.All(diagnostics, d => Assert.Equal("cross-database-transaction", d.Code));
        Assert.All(diagnostics, d => Assert.Contains("cross-database", d.Message, StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(@"
        BEGIN TRANSACTION
        INSERT INTO dbo.Table1 VALUES (1)
        INSERT INTO dbo.Table2 VALUES (2)
        COMMIT")]
    [InlineData(@"
        BEGIN TRANSACTION
        UPDATE MyDB.dbo.Users SET Name = 'Test'
        UPDATE MyDB.dbo.Orders SET Status = 'Done'
        COMMIT")]
    [InlineData(@"
        INSERT INTO DB1.dbo.Table1 VALUES (1)
        INSERT INTO DB2.dbo.Table2 VALUES (2)")]
    [InlineData("SELECT * FROM DB1.dbo.Table1")]
    public void Analyze_WhenNoIssue_ReturnsNoDiagnostic(string sql)
    {
        var rule = new CrossDatabaseTransactionRule();
        var context = CreateContext(sql);
        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_WhenNestedTransaction_DetectsCrossDatabase()
    {
        var sql = @"
            BEGIN TRANSACTION
                INSERT INTO DB1.dbo.Table1 VALUES (1)
                BEGIN TRANSACTION
                    UPDATE DB2.dbo.Table2 SET Value = 1
                COMMIT
            COMMIT
        ";

        var rule = new CrossDatabaseTransactionRule();
        var context = CreateContext(sql);
        var diagnostics = rule.Analyze(context).ToArray();

        Assert.NotEmpty(diagnostics);
        Assert.All(diagnostics, d => Assert.Equal("cross-database-transaction", d.Code));
    }

    [Fact]
    public void Analyze_WhenCrossDatabaseTransactionWithoutTermination_ReturnsDiagnostic()
    {
        var sql = @"
            BEGIN TRANSACTION
                INSERT INTO DB1.dbo.Table1 VALUES (1)
                INSERT INTO DB2.dbo.Table2 VALUES (2)
        ";

        var rule = new CrossDatabaseTransactionRule();
        var context = CreateContext(sql);
        var diagnostics = rule.Analyze(context).ToArray();

        Assert.NotEmpty(diagnostics);
        Assert.All(diagnostics, d => Assert.Equal("cross-database-transaction", d.Code));
    }

    [Fact]
    public void Analyze_WhenCrossDatabaseReferenceAppearsInFromJoin_ReturnsDiagnostic()
    {
        var sql = @"
            BEGIN TRANSACTION
                UPDATE u
                SET u.Name = o.Name
                FROM DB1.dbo.Users u
                INNER JOIN DB2.dbo.Orders o ON u.Id = o.UserId
            COMMIT
        ";

        var rule = new CrossDatabaseTransactionRule();
        var context = CreateContext(sql);
        var diagnostics = rule.Analyze(context).ToArray();

        Assert.NotEmpty(diagnostics);
        Assert.All(diagnostics, d => Assert.Equal("cross-database-transaction", d.Code));
    }

    [Fact]
    public void GetFixes_ReturnsNoFixes()
    {
        var sql = @"
            BEGIN TRANSACTION
            INSERT INTO DB1.dbo.Table1 VALUES (1)
            INSERT INTO DB2.dbo.Table2 VALUES (2)
            COMMIT";

        var rule = new CrossDatabaseTransactionRule();
        var context = CreateContext(sql);
        var diagnostics = rule.Analyze(context);
        var diagnostic = diagnostics.First();
        var fixes = rule.GetFixes(context, diagnostic).ToArray();

        Assert.Empty(fixes);
    }

    private static RuleContext CreateContext(string sql)
    {
        return RuleTestContext.CreateContext(sql);
    }
}
