using TsqlRefine.Rules.Rules.Schema;
using TsqlRefine.Rules.Tests.Helpers;
using TsqlRefine.Schema.Resolution;
using TsqlRefine.Schema.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Schema;

public sealed class DeleteColumnNotInTableRuleTests
{
    private readonly DeleteColumnNotInTableRule _rule = new();

    private static SchemaProvider CreateSchema() =>
        new(TestSchemaBuilder.Create()
            .AddTable("dbo", "Users", t => t
                .AddColumn("Id", "int")
                .AddColumn("Name", "nvarchar", maxLength: 100)
                .AddColumn("Email", "nvarchar", maxLength: 200))
            .AddTable("dbo", "Orders", t => t
                .AddColumn("Id", "int")
                .AddColumn("UserId", "int")
                .AddColumn("Total", "decimal", precision: 18, scale: 2))
            .Build());

    // === Positive cases (violations) ===

    [Fact]
    public void Analyze_SimpleDeleteBadWhereColumn_ReturnsDiagnostic()
    {
        const string sql = "DELETE FROM dbo.Users WHERE BadCol = 1;";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("delete-column-not-in-table", diagnostics[0].Code);
        Assert.Contains("BadCol", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_QualifiedBadColumn_ReturnsDiagnostic()
    {
        const string sql = "DELETE u FROM dbo.Users AS u WHERE u.BadCol = 1;";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Contains("BadCol", diagnostics[0].Message);
        Assert.Contains("dbo.Users", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_MultipleBadColumns_ReturnsMultipleDiagnostics()
    {
        const string sql = "DELETE FROM dbo.Users WHERE Bad1 = 1 AND Bad2 = 2;";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Equal(2, diagnostics.Length);
    }

    [Fact]
    public void Analyze_BadColumnFromJoinedTable_ReturnsDiagnostic()
    {
        const string sql = """
            DELETE u
            FROM dbo.Users AS u
            INNER JOIN dbo.Orders AS o ON u.Id = o.UserId
            WHERE o.BadCol = 1;
            """;
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Contains("BadCol", diagnostics[0].Message);
        Assert.Contains("dbo.Orders", diagnostics[0].Message);
    }

    // === Negative cases (no false positives) ===

    [Fact]
    public void Analyze_ValidSimpleDelete_ReturnsEmpty()
    {
        const string sql = "DELETE FROM dbo.Users WHERE Id = 1;";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_ValidMultiTableDelete_ReturnsEmpty()
    {
        const string sql = """
            DELETE u
            FROM dbo.Users AS u
            INNER JOIN dbo.Orders AS o ON u.Id = o.UserId
            WHERE u.Name = 'x';
            """;
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_ValidColumnFromJoinedTableInWhere_ReturnsEmpty()
    {
        const string sql = """
            DELETE u
            FROM dbo.Users AS u
            INNER JOIN dbo.Orders AS o ON u.Id = o.UserId
            WHERE o.Total > 100;
            """;
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_TempTable_ReturnsEmpty()
    {
        const string sql = "DELETE FROM #Temp WHERE Anything = 1;";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_TableVariable_ReturnsEmpty()
    {
        const string sql = "DELETE FROM @TableVar WHERE Anything = 1;";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_UnresolvedTable_ReturnsEmpty()
    {
        const string sql = "DELETE FROM dbo.NonExistent WHERE Col1 = 1;";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_NoSchema_ReturnsEmpty()
    {
        var context = RuleTestContext.CreateContext("DELETE FROM dbo.Users WHERE BadCol = 1;");

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_CaseInsensitive_ReturnsEmpty()
    {
        const string sql = "DELETE FROM dbo.Users WHERE ID = 1;";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_DeleteWithoutWhere_ReturnsEmpty()
    {
        const string sql = "DELETE FROM dbo.Users;";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_SubqueryInWhere_DoesNotValidateSubqueryColumns()
    {
        const string sql = """
            DELETE FROM dbo.Users
            WHERE Id IN (SELECT UserId FROM dbo.Orders);
            """;
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_ExistsInWhere_DoesNotValidateSubqueryColumns()
    {
        const string sql = """
            DELETE FROM dbo.Users
            WHERE EXISTS (SELECT 1 FROM dbo.Orders WHERE dbo.Orders.UserId = dbo.Users.Id);
            """;
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }
}
