using TsqlRefine.Rules.Rules.Schema;
using TsqlRefine.Rules.Tests.Helpers;
using TsqlRefine.Schema.Resolution;
using TsqlRefine.Schema.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Schema;

public sealed class UpdateJoinCardinalityMismatchRuleTests
{
    private readonly UpdateJoinCardinalityMismatchRule _rule = new();

    private static SchemaProvider CreateSchema() =>
        new(TestSchemaBuilder.Create()
            .AddTable("dbo", "Orders", t => t
                .AddColumn("OrderId", "int")
                .AddColumn("CustomerId", "int")
                .AddColumn("Status", "nvarchar", maxLength: 50)
                .AddColumn("Amount", "decimal", precision: 18, scale: 2)
                .WithPrimaryKey(true, "OrderId")
                .AddForeignKey("FK_Orders_Customers", ["CustomerId"], "dbo", "Customers", ["CustomerId"]))
            .AddTable("dbo", "Customers", t => t
                .AddColumn("CustomerId", "int")
                .AddColumn("Name", "nvarchar", maxLength: 100)
                .AddColumn("Region", "nvarchar", maxLength: 50)
                .WithPrimaryKey(true, "CustomerId"))
            .AddTable("dbo", "OrderItems", t => t
                .AddColumn("ItemId", "int")
                .AddColumn("OrderId", "int")
                .AddColumn("ProductId", "int")
                .AddColumn("Quantity", "int")
                .WithPrimaryKey(true, "ItemId")
                .AddForeignKey("FK_Items_Orders", ["OrderId"], "dbo", "Orders", ["OrderId"]))
            .AddTable("dbo", "Products", t => t
                .AddColumn("ProductId", "int")
                .AddColumn("Name", "nvarchar", maxLength: 200)
                .AddColumn("Price", "decimal", precision: 18, scale: 2)
                .WithPrimaryKey(true, "ProductId"))
            .AddTable("dbo", "OrderLog", t => t
                .AddColumn("LogId", "int")
                .AddColumn("OrderId", "int")
                .AddColumn("Action", "nvarchar", maxLength: 100)
                .AddColumn("LogDate", "datetime2")
                .WithPrimaryKey(true, "LogId"))
            .AddTable("dbo", "OrderSummary", t => t
                .AddColumn("OrderId", "int")
                .AddColumn("TotalAmount", "decimal", precision: 18, scale: 2)
                .WithPrimaryKey(true, "OrderId"))
            .AddTable("dbo", "OrderItemPricing", t => t
                .AddColumn("OrderId", "int")
                .AddColumn("ProductId", "int")
                .AddColumn("UnitPrice", "decimal", precision: 18, scale: 2)
                .AddUniqueConstraint("UQ_OIP", "OrderId", "ProductId"))
            .Build());

    // ===== Positive cases: should detect violations =====

    [Fact]
    public void Analyze_UpdateJoinOneToMany_ReturnsDiagnostic()
    {
        const string sql = """
            UPDATE o SET o.Amount = oi.Quantity * 10
            FROM dbo.Orders AS o
            INNER JOIN dbo.OrderItems AS oi ON oi.OrderId = o.OrderId;
            """;
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("update-join-cardinality-mismatch", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_UpdateJoinOneToManyAliasTarget_ReturnsDiagnostic()
    {
        const string sql = """
            UPDATE o SET o.Amount = oi.Quantity
            FROM dbo.Orders o
            INNER JOIN dbo.OrderItems oi ON oi.OrderId = o.OrderId;
            """;
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
    }

    [Fact]
    public void Analyze_UpdateLeftJoinOneToMany_ReturnsDiagnostic()
    {
        const string sql = """
            UPDATE o SET o.Status = 'logged'
            FROM dbo.Orders AS o
            LEFT JOIN dbo.OrderLog AS ol ON ol.OrderId = o.OrderId;
            """;
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
    }

    [Fact]
    public void Analyze_UpdateRightJoinOneToMany_ReturnsDiagnostic()
    {
        const string sql = """
            UPDATE o SET o.Status = 'logged'
            FROM dbo.OrderLog AS ol
            RIGHT JOIN dbo.Orders AS o ON ol.OrderId = o.OrderId;
            """;
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
    }

    [Fact]
    public void Analyze_UpdateManyToMany_ReturnsDiagnostic()
    {
        const string sql = """
            UPDATE o SET o.Status = 'has-log'
            FROM dbo.Orders AS o
            INNER JOIN dbo.OrderLog AS ol ON ol.Action = o.Status;
            """;
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
    }

    [Fact]
    public void Analyze_UpdateSelfJoinNonUniqueColumn_ReturnsDiagnostic()
    {
        const string sql = """
            UPDATE o1 SET o1.Amount = o2.Amount
            FROM dbo.Orders AS o1
            INNER JOIN dbo.Orders AS o2 ON o2.CustomerId = o1.CustomerId;
            """;
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
    }

    [Fact]
    public void Analyze_UpdateMultipleJoinsOneProblematic_ReturnsSingleDiagnostic()
    {
        const string sql = """
            UPDATE o SET o.Amount = oi.Quantity * p.Price
            FROM dbo.Orders AS o
            INNER JOIN dbo.Customers AS c ON c.CustomerId = o.CustomerId
            INNER JOIN dbo.OrderItems AS oi ON oi.OrderId = o.OrderId
            INNER JOIN dbo.Products AS p ON p.ProductId = oi.ProductId;
            """;
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Contains("OrderItems", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_UpdateUnqualifiedTarget_ReturnsDiagnostic()
    {
        const string sql = """
            UPDATE Orders SET Amount = oi.Quantity * 10
            FROM dbo.Orders
            INNER JOIN dbo.OrderItems AS oi ON oi.OrderId = Orders.OrderId;
            """;
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
    }

    [Fact]
    public void Analyze_DiagnosticMessageContainsTableNames()
    {
        const string sql = """
            UPDATE o SET o.Amount = oi.Quantity
            FROM dbo.Orders AS o
            INNER JOIN dbo.OrderItems AS oi ON oi.OrderId = o.OrderId;
            """;
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Contains("OrderItems", diagnostics[0].Message);
        Assert.Contains("Orders", diagnostics[0].Message);
    }

    // ===== Negative cases: should NOT detect =====

    [Fact]
    public void Analyze_UpdateJoinOneToOne_ReturnsEmpty()
    {
        const string sql = """
            UPDATE o SET o.Amount = s.TotalAmount
            FROM dbo.Orders AS o
            INNER JOIN dbo.OrderSummary AS s ON s.OrderId = o.OrderId;
            """;
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_UpdateJoinManyToOne_ReturnsEmpty()
    {
        const string sql = """
            UPDATE o SET o.Status = c.Name
            FROM dbo.Orders AS o
            INNER JOIN dbo.Customers AS c ON c.CustomerId = o.CustomerId;
            """;
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_SimpleUpdateWithoutFrom_ReturnsEmpty()
    {
        const string sql = "UPDATE dbo.Orders SET Status = 'done' WHERE OrderId = 1;";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_NoSchema_ReturnsEmpty()
    {
        const string sql = """
            UPDATE o SET o.Amount = oi.Quantity
            FROM dbo.Orders AS o
            INNER JOIN dbo.OrderItems AS oi ON oi.OrderId = o.OrderId;
            """;
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_CrossJoin_ReturnsEmpty()
    {
        const string sql = """
            UPDATE o SET o.Status = 'crossed'
            FROM dbo.Orders AS o
            CROSS JOIN dbo.Customers AS c;
            """;
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_TempTable_ReturnsEmpty()
    {
        const string sql = """
            UPDATE #Temp SET val = oi.Quantity
            FROM #Temp
            INNER JOIN dbo.OrderItems AS oi ON oi.OrderId = #Temp.OrderId;
            """;
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_DerivedTable_ReturnsEmpty()
    {
        const string sql = """
            UPDATE o SET o.Amount = sub.Total
            FROM dbo.Orders AS o
            INNER JOIN (SELECT OrderId, SUM(Quantity) AS Total FROM dbo.OrderItems GROUP BY OrderId) AS sub
                ON sub.OrderId = o.OrderId;
            """;
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_UnresolvedTable_ReturnsEmpty()
    {
        const string sql = """
            UPDATE o SET o.Amount = x.val
            FROM dbo.Orders AS o
            INNER JOIN dbo.NonExistent AS x ON x.OrderId = o.OrderId;
            """;
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_SelectWithJoin_ReturnsEmpty()
    {
        const string sql = """
            SELECT o.OrderId, oi.Quantity
            FROM dbo.Orders AS o
            INNER JOIN dbo.OrderItems AS oi ON oi.OrderId = o.OrderId;
            """;
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_CompositeUniqueConstraintCoversJoin_ReturnsEmpty()
    {
        const string sql = """
            UPDATE oi SET oi.Quantity = oip.UnitPrice
            FROM dbo.OrderItems AS oi
            INNER JOIN dbo.OrderItemPricing AS oip
                ON oip.OrderId = oi.OrderId AND oip.ProductId = oi.ProductId;
            """;
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }
}
