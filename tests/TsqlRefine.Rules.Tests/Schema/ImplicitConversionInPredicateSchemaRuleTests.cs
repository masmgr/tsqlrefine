using TsqlRefine.Rules.Rules.Schema;
using TsqlRefine.Rules.Tests.Helpers;
using TsqlRefine.Schema.Resolution;
using TsqlRefine.Schema.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Schema;

public sealed class ImplicitConversionInPredicateSchemaRuleTests
{
    private readonly ImplicitConversionInPredicateSchemaRule _rule = new();

    private static SchemaProvider CreateSchema() =>
        new(TestSchemaBuilder.Create()
            .AddTable("dbo", "Users", t => t
                .AddColumn("Id", "int")
                .AddColumn("Name", "nvarchar", maxLength: 100)
                .AddColumn("Email", "varchar", maxLength: 200)
                .AddColumn("CreatedAt", "datetime"))
            .AddTable("dbo", "Orders", t => t
                .AddColumn("Id", "int")
                .AddColumn("UserId", "int")
                .AddColumn("Total", "decimal", precision: 18, scale: 2)
                .AddColumn("OrderDate", "datetime2")
                .AddColumn("Code", "int"))
            .AddTable("sales", "Orders", t => t
                .AddColumn("Id", "int")
                .AddColumn("Code", "varchar", maxLength: 20))
            .Build());

    [Theory]
    [InlineData("SELECT u.Id FROM dbo.Users AS u WHERE u.Id = 1;")]
    [InlineData("SELECT u.Name FROM dbo.Users AS u WHERE u.Name = N'Test';")]
    [InlineData("SELECT u.Email FROM dbo.Users AS u WHERE u.Email = 'test';")]
    [InlineData("SELECT o.Total FROM dbo.Orders AS o WHERE o.Total = 100.50;")]
    public void Analyze_CompatibleTypes_ReturnsNoDiagnostics(string sql)
    {
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_VarcharColumnWithIntLiteral_ReturnsDiagnostic()
    {
        // varchar column compared to int literal — column (varchar) gets converted
        const string sql = "SELECT u.Email FROM dbo.Users AS u WHERE u.Email = 1;";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("implicit-conversion-in-predicate-schema", diagnostics[0].Code);
        Assert.Contains("Email", diagnostics[0].Message);
        Assert.Contains("varchar", diagnostics[0].Message);
        Assert.Contains("int", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_VarcharColumnWithNvarcharLiteral_ReturnsDiagnostic()
    {
        // varchar column compared to N'...' (nvarchar) — varchar column is converted
        const string sql = "SELECT u.Email FROM dbo.Users AS u WHERE u.Email = N'test';";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("implicit-conversion-in-predicate-schema", diagnostics[0].Code);
        Assert.Contains("Email", diagnostics[0].Message);
        Assert.Contains("varchar", diagnostics[0].Message);
        Assert.Contains("nvarchar", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_IntColumnWithStringLiteral_NoDiagnostic()
    {
        // int column vs varchar literal — literal side (varchar) is converted, not column
        const string sql = "SELECT u.Id FROM dbo.Users AS u WHERE u.Id = '1';";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_DatetimeColumnWithVarcharLiteral_NoDiagnostic()
    {
        // datetime column vs varchar literal — literal is converted, not column
        const string sql = "SELECT u.Id FROM dbo.Users AS u WHERE u.CreatedAt = '2024-01-01';";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_JoinWithTypeMismatch_ReturnsDiagnostic()
    {
        // int column joined with decimal column — int is converted
        const string sql = """
            SELECT u.Id
            FROM dbo.Users AS u
            INNER JOIN dbo.Orders AS o ON u.Id = o.Total;
            """;
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Contains("Id", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_JoinWithCompatibleTypes_NoDiagnostic()
    {
        const string sql = """
            SELECT u.Id
            FROM dbo.Users AS u
            INNER JOIN dbo.Orders AS o ON u.Id = o.UserId;
            """;
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_NoSchema_ReturnsNoDiagnostics()
    {
        const string sql = "SELECT u.Email FROM dbo.Users AS u WHERE u.Email = 1;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_NoFromClause_ReturnsNoDiagnostics()
    {
        const string sql = "SELECT 1 WHERE 1 = '1';";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_TempTable_SkipsValidation()
    {
        const string sql = "SELECT t.Col FROM #Temp AS t WHERE t.Col = 1;";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_DatetimeVsDatetime2_ReturnsDiagnostic()
    {
        // datetime column compared to datetime2 column — datetime is converted
        const string sql = """
            SELECT u.Id
            FROM dbo.Users AS u
            INNER JOIN dbo.Orders AS o ON u.CreatedAt = o.OrderDate;
            """;
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Contains("CreatedAt", diagnostics[0].Message);
        Assert.Contains("datetime", diagnostics[0].Message);
        Assert.Contains("datetime2", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_UnqualifiedColumn_ResolvedCorrectly()
    {
        const string sql = "SELECT Email FROM dbo.Users WHERE Email = 1;";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Contains("Email", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_NvarcharColumnWithVarcharLiteral_NoDiagnostic()
    {
        // nvarchar column vs varchar literal — literal is converted, not column
        const string sql = "SELECT u.Name FROM dbo.Users AS u WHERE u.Name = 'test';";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_MultipleComparisons_ReportsEach()
    {
        const string sql = """
            SELECT u.Id
            FROM dbo.Users AS u
            WHERE u.Email = 1 AND u.Email = N'test';
            """;
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Equal(2, diagnostics.Length);
        Assert.All(diagnostics, d => Assert.Equal("implicit-conversion-in-predicate-schema", d.Code));
    }

    [Fact]
    public void Analyze_SchemaQualifiedColumn_UsesQualifiedTableType()
    {
        const string sql = """
            SELECT sales.Orders.Code
            FROM dbo.Orders
            INNER JOIN sales.Orders ON dbo.Orders.Id = sales.Orders.Id
            WHERE sales.Orders.Code = 1;
            """;
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("implicit-conversion-in-predicate-schema", diagnostics[0].Code);
        Assert.Contains("sales.Orders.Code", diagnostics[0].Message);
    }
}
