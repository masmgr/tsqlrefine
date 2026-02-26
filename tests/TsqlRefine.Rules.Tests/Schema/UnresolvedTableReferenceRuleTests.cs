using TsqlRefine.Rules.Rules.Schema;
using TsqlRefine.Rules.Tests.Helpers;
using TsqlRefine.Schema.Resolution;
using TsqlRefine.Schema.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Schema;

public sealed class UnresolvedTableReferenceRuleTests
{
    private readonly UnresolvedTableReferenceRule _rule = new();

    private static SchemaProvider CreateSchema() =>
        new(TestSchemaBuilder.Create()
            .AddTable("dbo", "Users", t => t
                .AddColumn("Id", "int")
                .AddColumn("Name", "nvarchar", maxLength: 100))
            .AddTable("dbo", "Orders", t => t
                .AddColumn("Id", "int")
                .AddColumn("UserId", "int")
                .AddColumn("Total", "decimal", precision: 18, scale: 2))
            .AddTable("sales", "Products", t => t
                .AddColumn("Id", "int")
                .AddColumn("Name", "nvarchar", maxLength: 200))
            .Build());

    [Theory]
    [InlineData("SELECT * FROM dbo.Users;")]
    [InlineData("SELECT * FROM Users;")]
    [InlineData("SELECT * FROM dbo.Orders;")]
    [InlineData("SELECT * FROM sales.Products;")]
    public void Analyze_ExistingTable_ReturnsNoDiagnostics(string sql)
    {
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Theory]
    [InlineData("SELECT * FROM dbo.NonExistent;")]
    [InlineData("SELECT * FROM Missing;")]
    public void Analyze_MissingTable_ReturnsDiagnostic(string sql)
    {
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("unresolved-table-reference", diagnostics[0].Code);
        Assert.Contains("not found in schema snapshot", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_CteReference_SkipsValidation()
    {
        const string sql = """
            WITH cte AS (SELECT 1 AS Id)
            SELECT c.Id
            FROM cte AS c;
            """;
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Theory]
    [InlineData("SELECT * FROM #TempTable;")]
    [InlineData("SELECT * FROM ##GlobalTemp;")]
    [InlineData("SELECT * FROM @TableVar;")]
    public void Analyze_TempTableOrTableVariable_SkipsValidation(string sql)
    {
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Theory]
    [InlineData("SELECT * FROM sys.objects;")]
    [InlineData("SELECT * FROM INFORMATION_SCHEMA.TABLES;")]
    public void Analyze_SystemSchema_SkipsValidation(string sql)
    {
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_NoSchema_ReturnsNoDiagnostics()
    {
        var context = RuleTestContext.CreateContext("SELECT * FROM NonExistent;");

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_CaseInsensitive_ReturnsNoDiagnostics()
    {
        var context = RuleTestContext.CreateContext("SELECT * FROM DBO.USERS;", CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_ThreePartName_ResolvesCorrectly()
    {
        var context = RuleTestContext.CreateContext(
            "SELECT * FROM TestDb.dbo.Users;", CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_JoinWithMissingTable_ReturnsDiagnostic()
    {
        const string sql = """
            SELECT u.Id
            FROM dbo.Users AS u
            INNER JOIN dbo.Missing AS m ON u.Id = m.UserId;
            """;
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Contains("Missing", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_UpdateAliasTarget_SkipsValidation()
    {
        const string sql = """
            UPDATE u
            SET Name = N'Updated'
            FROM dbo.Users AS u;
            """;
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }
}
