using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Correctness;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Correctness;

public sealed class AvoidNotInWithNullRuleTests
{
    private readonly AvoidNotInWithNullRule _rule = new();

    [Fact]
    public void Metadata_HasCorrectProperties()
    {
        Assert.Equal("avoid-not-in-with-null", _rule.Metadata.RuleId);
        Assert.Equal("Correctness", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Warning, _rule.Metadata.DefaultSeverity);
        Assert.False(_rule.Metadata.Fixable);
    }

    [Fact]
    public void Analyze_NotInWithSubquery_ReturnsDiagnostic()
    {
        const string sql = "SELECT * FROM dbo.Orders WHERE CustomerId NOT IN (SELECT CustomerId FROM dbo.Blacklist);";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("avoid-not-in-with-null", diagnostics[0].Code);
        Assert.Contains("NOT IN", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_NotInWithSubqueryInWhere_ReturnsDiagnostic()
    {
        const string sql = @"
            SELECT o.OrderId, o.CustomerId
            FROM dbo.Orders AS o
            WHERE o.CustomerId NOT IN (
                SELECT c.CustomerId FROM dbo.Customers AS c WHERE c.IsActive = 0
            );
        ";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("avoid-not-in-with-null", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_MultipleNotInSubqueries_ReturnsMultipleDiagnostics()
    {
        const string sql = @"
            SELECT *
            FROM dbo.Products
            WHERE CategoryId NOT IN (SELECT CategoryId FROM dbo.DisabledCategories)
              AND SupplierId NOT IN (SELECT SupplierId FROM dbo.BlockedSuppliers);
        ";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Equal(2, diagnostics.Length);
        Assert.All(diagnostics, d => Assert.Equal("avoid-not-in-with-null", d.Code));
    }

    [Fact]
    public void Analyze_NotInWithSubqueryInDelete_ReturnsDiagnostic()
    {
        const string sql = @"
            DELETE FROM dbo.Orders
            WHERE CustomerId NOT IN (SELECT CustomerId FROM dbo.Customers);
        ";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("avoid-not-in-with-null", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_InWithSubquery_NoDiagnostic()
    {
        const string sql = "SELECT * FROM dbo.Orders WHERE CustomerId IN (SELECT CustomerId FROM dbo.Customers);";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_NotInWithValueList_NoDiagnostic()
    {
        const string sql = "SELECT * FROM dbo.Orders WHERE Status NOT IN ('Cancelled', 'Refunded', 'Error');";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_InWithValueList_NoDiagnostic()
    {
        const string sql = "SELECT * FROM dbo.Orders WHERE Status IN (1, 2, 3);";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Theory]
    [InlineData("SELECT * FROM dbo.Orders WHERE NOT EXISTS (SELECT 1 FROM dbo.Blacklist WHERE Blacklist.Id = Orders.Id);")]
    [InlineData("SELECT * FROM dbo.Orders o WHERE o.Id NOT IN (1, 2, 3);")]
    [InlineData("SELECT * FROM dbo.Orders EXCEPT SELECT * FROM dbo.CancelledOrders;")]
    [InlineData("")]
    public void Analyze_SafeAlternatives_NoDiagnostic(string sql)
    {
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_NotInSubqueryInHaving_ReturnsDiagnostic()
    {
        const string sql = @"
            SELECT CategoryId, COUNT(*)
            FROM dbo.Products
            GROUP BY CategoryId
            HAVING CategoryId NOT IN (SELECT CategoryId FROM dbo.HiddenCategories);
        ";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("avoid-not-in-with-null", diagnostics[0].Code);
    }

    [Fact]
    public void GetFixes_ReturnsEmptyCollection()
    {
        const string sql = "SELECT * FROM dbo.Orders WHERE CustomerId NOT IN (SELECT CustomerId FROM dbo.Blacklist);";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostic = _rule.Analyze(context).First();

        var fixes = _rule.GetFixes(context, diagnostic);

        Assert.Empty(fixes);
    }
}
