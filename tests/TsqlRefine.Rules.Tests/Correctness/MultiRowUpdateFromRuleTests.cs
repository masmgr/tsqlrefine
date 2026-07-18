using TsqlRefine.Rules.Rules.Correctness;
using TsqlRefine.Rules.Tests.Helpers;
using TsqlRefine.Schema.Resolution;
using TsqlRefine.Schema.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Correctness;

public sealed class MultiRowUpdateFromRuleTests
{
    private readonly MultiRowUpdateFromRule _rule = new();

    [Fact]
    public void Analyze_UpdateFromInnerJoin_ReturnsDiagnostic()
    {
        const string sql = """
            UPDATE o SET o.Amount = oi.Quantity * 10
            FROM dbo.Orders AS o
            INNER JOIN dbo.OrderItems AS oi ON oi.OrderId = o.OrderId;
            """;
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("multi-row-update-from", diagnostic.Code);
    }

    [Fact]
    public void Analyze_UpdateFromLeftJoin_ReturnsDiagnostic()
    {
        const string sql = """
            UPDATE o SET o.Status = 'logged'
            FROM dbo.Orders AS o
            LEFT JOIN dbo.OrderLog AS ol ON ol.OrderId = o.OrderId;
            """;
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
    }

    [Fact]
    public void Analyze_UpdateFromMultipleJoins_ReturnsSingleDiagnostic()
    {
        const string sql = """
            UPDATE o SET o.Total = oi.Quantity * p.Price
            FROM dbo.Orders AS o
            INNER JOIN dbo.OrderItems AS oi ON oi.OrderId = o.OrderId
            INNER JOIN dbo.Products AS p ON p.ProductId = oi.ProductId;
            """;
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
    }

    [Fact]
    public void Analyze_UpdateFromCrossJoin_ReturnsDiagnostic()
    {
        const string sql = """
            UPDATE o SET o.Status = s.Status
            FROM dbo.Orders AS o
            CROSS JOIN dbo.StatusSource AS s;
            """;
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
    }

    [Fact]
    public void Analyze_SimpleUpdateWithoutFrom_ReturnsEmpty()
    {
        const string sql = "UPDATE dbo.Orders SET Status = 'done' WHERE OrderId = 1;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_UpdateFromWithoutJoin_ReturnsEmpty()
    {
        const string sql = """
            UPDATE o SET o.Status = 'pending'
            FROM dbo.Orders AS o
            WHERE o.Amount IS NULL;
            """;
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_TempTableTarget_ReturnsDiagnostic()
    {
        const string sql = """
            UPDATE t SET t.Value = s.Value
            FROM #Temp AS t
            INNER JOIN dbo.Source AS s ON s.Id = t.Id;
            """;
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
    }

    [Fact]
    public void Analyze_TableVariableTarget_ReturnsDiagnostic()
    {
        const string sql = """
            UPDATE t SET t.Value = s.Value
            FROM @Target AS t
            INNER JOIN dbo.Source AS s ON s.Id = t.Id;
            """;
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
    }

    [Fact]
    public void Analyze_WithSchemaContextStillReturnsDiagnostic()
    {
        const string sql = """
            UPDATE o SET o.Amount = oi.Quantity * 10
            FROM dbo.Orders AS o
            INNER JOIN dbo.OrderItems AS oi ON oi.OrderId = o.OrderId;
            """;
        var schema = new SchemaProvider(TestSchemaBuilder.Create()
            .AddTable("dbo", "Orders", t => t
                .AddColumn("OrderId", "int")
                .AddColumn("Amount", "decimal", precision: 18, scale: 2)
                .WithPrimaryKey(true, "OrderId"))
            .AddTable("dbo", "OrderItems", t => t
                .AddColumn("ItemId", "int")
                .AddColumn("OrderId", "int")
                .AddColumn("Quantity", "int")
                .WithPrimaryKey(true, "ItemId"))
            .Build());
        var context = RuleTestContext.CreateContext(sql, schema);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
    }
}
