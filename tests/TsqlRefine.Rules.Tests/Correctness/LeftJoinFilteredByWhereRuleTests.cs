using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Correctness.Semantic;
using TsqlRefine.Rules.Tests.Helpers;
using TsqlRefine.Schema.Resolution;
using TsqlRefine.Schema.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Correctness;

public sealed class LeftJoinFilteredByWhereRuleTests
{
    private static SchemaProvider CreateSchema() =>
        new(TestSchemaBuilder.Create()
            .AddTable("dbo", "Orders", t => t
                .AddColumn("OrderId", "int")
                .WithPrimaryKey(true, "OrderId")
                .AddColumn("CustomerId", "int")
                .AddColumn("Status", "varchar", maxLength: 50)
                .AddForeignKey("FK_Orders_Customers", ["CustomerId"], "dbo", "Customers", ["CustomerId"]))
            .AddTable("dbo", "Customers", t => t
                .AddColumn("CustomerId", "int")
                .WithPrimaryKey(true, "CustomerId")
                .AddColumn("Name", "nvarchar", maxLength: 200)
                .AddColumn("Email", "varchar", maxLength: 200, nullable: true))
            .AddTable("dbo", "OrderDetails", t => t
                .AddColumn("DetailId", "int")
                .WithPrimaryKey(true, "DetailId")
                .AddColumn("OrderId", "int")
                .AddColumn("Quantity", "int")
                .AddColumn("Discount", "decimal", precision: 5, scale: 2, nullable: true))
            .Build());
    [Theory]
    [InlineData("SELECT * FROM t1 LEFT JOIN t2 ON t1.id = t2.id WHERE t2.status = 1")]  // filters right-side
    [InlineData("SELECT * FROM t1 LEFT JOIN t2 ON t1.id = t2.id WHERE t2.id IN (1,2,3)")]  // IN clause on right-side
    [InlineData("SELECT * FROM t1 LEFT JOIN t2 ON t1.id = t2.id WHERE t2.name = 'test'")]  // string comparison
    [InlineData("SELECT * FROM t1 a LEFT JOIN t2 b ON a.id = b.id WHERE b.status = 1")]  // with aliases
    [InlineData("SELECT * FROM t1 LEFT JOIN t2 ON t1.id = t2.id WHERE t2.active = 1")]  // boolean field
    [InlineData("SELECT * FROM t1 LEFT JOIN t2 ON t1.id = t2.id WHERE t1.id = 1 AND t2.status > 0")]  // AND with right-side comparison
    [InlineData("SELECT * FROM t1 LEFT JOIN t2 ON t1.id = t2.id WHERE t2.status = 1 OR t2.status = 2")]  // OR where both branches still filter right-side table
    public void Analyze_WhenLeftJoinFilteredByWhere_ReturnsDiagnostic(string sql)
    {
        var rule = new LeftJoinFilteredByWhereRule();
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.NotEmpty(diagnostics);
        Assert.Contains(diagnostics, d => d.Data?.RuleId == "semantic-left-join-filtered-by-where");
        Assert.All(diagnostics.Where(d => d.Data?.RuleId == "semantic-left-join-filtered-by-where"), d =>
        {
            Assert.Equal("Correctness", d.Data?.Category);
            Assert.False(d.Data?.Fixable);
            Assert.Contains("LEFT JOIN", d.Message, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Theory]
    [InlineData("SELECT * FROM t1 LEFT JOIN t2 ON t1.id = t2.id WHERE t1.status = 1")]  // filters left side
    [InlineData("SELECT * FROM t1 LEFT JOIN t2 ON t1.id = t2.id WHERE t2.id IS NOT NULL")]  // intentional IS NOT NULL
    [InlineData("SELECT * FROM t1 LEFT JOIN t2 ON t1.id = t2.id WHERE t2.id IS NULL")]  // IS NULL (keeps LEFT JOIN semantic)
    [InlineData("SELECT * FROM t1 INNER JOIN t2 ON t1.id = t2.id WHERE t2.status = 1")]  // INNER JOIN (not LEFT)
    [InlineData("SELECT * FROM t1 LEFT JOIN t2 ON t1.id = t2.id")]  // no WHERE clause
    [InlineData("SELECT * FROM t1 a LEFT JOIN t2 b ON a.id = b.id WHERE a.status = 1")]  // filters left side with alias
    [InlineData("SELECT * FROM t1 a LEFT JOIN t2 b ON a.id = b.id WHERE a.id = 1 AND b.id IS NOT NULL")]  // explicit NULL check
    [InlineData("SELECT * FROM t1 LEFT JOIN t2 ON t1.id = t2.id WHERE t2.status = 1 OR t1.kind = 'A'")]  // OR condition can preserve NULL-extended rows
    public void Analyze_WhenLeftJoinNotFilteredByWhere_ReturnsEmpty(string sql)
    {
        var rule = new LeftJoinFilteredByWhereRule();
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).Where(d => d.Data?.RuleId == "semantic-left-join-filtered-by-where").ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_MultipleLeftJoins_OnlyReportsFiltered()
    {
        var rule = new LeftJoinFilteredByWhereRule();
        var sql = "SELECT * FROM t1 LEFT JOIN t2 ON t1.id = t2.id LEFT JOIN t3 ON t1.id = t3.id WHERE t2.status = 1";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).Where(d => d.Data?.RuleId == "semantic-left-join-filtered-by-where").ToArray();

        // Only t2 is filtered, not t3
        Assert.Single(diagnostics);
        Assert.Contains("t2", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_LeftJoinWithComplexWhere_DetectsFilter()
    {
        var rule = new LeftJoinFilteredByWhereRule();
        var sql = "SELECT * FROM t1 LEFT JOIN t2 ON t1.id = t2.id WHERE t1.active = 1 AND t2.status = 'active'";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).Where(d => d.Data?.RuleId == "semantic-left-join-filtered-by-where").ToArray();

        Assert.NotEmpty(diagnostics);
    }

    [Fact]
    public void Analyze_LikeOnRightSide_ReturnsDiagnostic()
    {
        var rule = new LeftJoinFilteredByWhereRule();
        var sql = "SELECT * FROM t1 LEFT JOIN t2 ON t1.id = t2.id WHERE t2.name LIKE '%test%'";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).Where(d => d.Data?.RuleId == "semantic-left-join-filtered-by-where").ToArray();

        // LIKE on right-side table negates LEFT JOIN (NULLs will fail LIKE)
        Assert.NotEmpty(diagnostics);
    }

    [Fact]
    public void Analyze_BetweenOnRightSide_ReturnsDiagnostic()
    {
        var rule = new LeftJoinFilteredByWhereRule();
        var sql = "SELECT * FROM t1 LEFT JOIN t2 ON t1.id = t2.id WHERE t2.val BETWEEN 1 AND 10";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).Where(d => d.Data?.RuleId == "semantic-left-join-filtered-by-where").ToArray();

        // BETWEEN on right-side table negates LEFT JOIN
        Assert.NotEmpty(diagnostics);
    }

    [Fact]
    public void Analyze_NotExpressionOnRightSide_ReturnsDiagnostic()
    {
        var rule = new LeftJoinFilteredByWhereRule();
        var sql = "SELECT * FROM t1 LEFT JOIN t2 ON t1.id = t2.id WHERE NOT t2.status = 0";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).Where(d => d.Data?.RuleId == "semantic-left-join-filtered-by-where").ToArray();

        // NOT on right-side filter still negates LEFT JOIN
        Assert.NotEmpty(diagnostics);
    }

    [Fact]
    public void Analyze_EmptyInput_ReturnsEmpty()
    {
        var rule = new LeftJoinFilteredByWhereRule();
        var context = RuleTestContext.CreateContext("");

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void GetFixes_ReturnsEmpty()
    {
        var rule = new LeftJoinFilteredByWhereRule();
        var context = RuleTestContext.CreateContext("SELECT * FROM t1 LEFT JOIN t2 ON t1.id = t2.id WHERE t2.status = 1");
        var diagnostic = new Diagnostic(
            Range: new TsqlRefine.PluginSdk.Range(new Position(0, 0), new Position(0, 10)),
            Message: "test",
            Code: "semantic-left-join-filtered-by-where"
        );

        var fixes = rule.GetFixes(context, diagnostic).ToArray();

        Assert.Empty(fixes);
    }

    [Fact]
    public void Metadata_HasCorrectValues()
    {
        var rule = new LeftJoinFilteredByWhereRule();

        Assert.Equal("semantic-left-join-filtered-by-where", rule.Metadata.RuleId);
        Assert.Equal("Correctness", rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Warning, rule.Metadata.DefaultSeverity);
        Assert.False(rule.Metadata.Fixable);
        Assert.Contains("LEFT JOIN", rule.Metadata.Description, StringComparison.OrdinalIgnoreCase);
    }

    // --- Schema-aware tests ---

    [Fact]
    public void Analyze_WithSchema_NotNullColumnFiltered_ReturnsDefinitiveMessage()
    {
        var rule = new LeftJoinFilteredByWhereRule();
        const string sql = "SELECT * FROM dbo.Orders o LEFT JOIN dbo.Customers c ON o.CustomerId = c.CustomerId WHERE c.Name = 'Test'";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = rule.Analyze(context).Where(d => d.Data?.RuleId == "semantic-left-join-filtered-by-where").ToArray();

        Assert.Single(diagnostics);
        Assert.Contains("definitively", diagnostics[0].Message);
        Assert.Contains("NOT NULL", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_WithSchema_NullableColumnFiltered_ReturnsStandardMessage()
    {
        var rule = new LeftJoinFilteredByWhereRule();
        const string sql = "SELECT * FROM dbo.Orders o LEFT JOIN dbo.Customers c ON o.CustomerId = c.CustomerId WHERE c.Email = 'test@example.com'";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = rule.Analyze(context).Where(d => d.Data?.RuleId == "semantic-left-join-filtered-by-where").ToArray();

        Assert.Single(diagnostics);
        Assert.DoesNotContain("definitively", diagnostics[0].Message);
        Assert.Contains("Consider using INNER JOIN instead", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_WithSchema_FkRelationshipAndNotNull_MentionsForeignKey()
    {
        var rule = new LeftJoinFilteredByWhereRule();
        const string sql = "SELECT * FROM dbo.Orders o LEFT JOIN dbo.Customers c ON o.CustomerId = c.CustomerId WHERE c.Name = 'Test'";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = rule.Analyze(context).Where(d => d.Data?.RuleId == "semantic-left-join-filtered-by-where").ToArray();

        Assert.Single(diagnostics);
        Assert.Contains("foreign key", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_WithSchema_NoFkButNotNull_ReturnsDefinitiveWithoutFk()
    {
        var rule = new LeftJoinFilteredByWhereRule();
        // OrderDetails has no FK to Customers, joining on non-FK columns
        const string sql = "SELECT * FROM dbo.Customers c LEFT JOIN dbo.OrderDetails d ON c.CustomerId = d.DetailId WHERE d.Quantity > 5";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = rule.Analyze(context).Where(d => d.Data?.RuleId == "semantic-left-join-filtered-by-where").ToArray();

        Assert.Single(diagnostics);
        Assert.Contains("definitively", diagnostics[0].Message);
        Assert.DoesNotContain("foreign key", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_NoSchema_RightSideFiltered_ReturnsGenericMessage()
    {
        var rule = new LeftJoinFilteredByWhereRule();
        const string sql = "SELECT * FROM dbo.Orders o LEFT JOIN dbo.Customers c ON o.CustomerId = c.CustomerId WHERE c.Name = 'Test'";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).Where(d => d.Data?.RuleId == "semantic-left-join-filtered-by-where").ToArray();

        Assert.Single(diagnostics);
        Assert.Contains("Consider using INNER JOIN instead", diagnostics[0].Message);
        Assert.DoesNotContain("definitively", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_WithSchema_IsNotNullOnRightSide_ReturnsEmpty()
    {
        var rule = new LeftJoinFilteredByWhereRule();
        const string sql = "SELECT * FROM dbo.Orders o LEFT JOIN dbo.Customers c ON o.CustomerId = c.CustomerId WHERE c.CustomerId IS NOT NULL";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = rule.Analyze(context).Where(d => d.Data?.RuleId == "semantic-left-join-filtered-by-where").ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_WithSchema_UnresolvableRightTable_ReturnsGenericMessage()
    {
        var rule = new LeftJoinFilteredByWhereRule();
        const string sql = "SELECT * FROM dbo.Orders o LEFT JOIN dbo.UnknownTable u ON o.OrderId = u.OrderId WHERE u.Status = 1";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = rule.Analyze(context).Where(d => d.Data?.RuleId == "semantic-left-join-filtered-by-where").ToArray();

        Assert.Single(diagnostics);
        Assert.Contains("Consider using INNER JOIN instead", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_WithSchema_NullableColumnBetween_ReturnsStandardMessage()
    {
        var rule = new LeftJoinFilteredByWhereRule();
        const string sql = "SELECT * FROM dbo.Orders o LEFT JOIN dbo.OrderDetails d ON o.OrderId = d.OrderId WHERE d.Discount BETWEEN 0.1 AND 0.5";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = rule.Analyze(context).Where(d => d.Data?.RuleId == "semantic-left-join-filtered-by-where").ToArray();

        Assert.Single(diagnostics);
        Assert.DoesNotContain("definitively", diagnostics[0].Message);
    }
}
