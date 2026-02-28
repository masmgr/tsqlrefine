using TsqlRefine.Rules.Rules.Schema;
using TsqlRefine.Rules.Tests.Helpers;
using TsqlRefine.Schema.Resolution;
using TsqlRefine.Schema.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Schema;

public sealed class UnresolvedColumnReferenceRuleTests
{
    private readonly UnresolvedColumnReferenceRule _rule = new();

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
            .AddTable("sales", "Orders", t => t
                .AddColumn("Id", "int")
                .AddColumn("SalesTotal", "decimal", precision: 18, scale: 2))
            .Build());

    [Theory]
    [InlineData("SELECT u.Id, u.Name FROM dbo.Users AS u;")]
    [InlineData("SELECT Id, Name FROM dbo.Users;")]
    [InlineData("SELECT o.Total FROM dbo.Orders AS o;")]
    public void Analyze_ExistingColumns_ReturnsNoDiagnostics(string sql)
    {
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_QualifiedMissingColumn_ReturnsDiagnostic()
    {
        const string sql = "SELECT u.NonExistent FROM dbo.Users AS u;";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("unresolved-column-reference", diagnostics[0].Code);
        Assert.Contains("NonExistent", diagnostics[0].Message);
        Assert.Contains("dbo.Users", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_UnqualifiedMissingColumn_ReturnsDiagnostic()
    {
        const string sql = "SELECT Nonexistent FROM dbo.Users;";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("unresolved-column-reference", diagnostics[0].Code);
        Assert.Contains("not found in any table", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_AmbiguousColumn_ReturnsDiagnostic()
    {
        const string sql = """
            SELECT Id
            FROM dbo.Users AS u
            INNER JOIN dbo.Orders AS o ON u.Id = o.Id;
            """;
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("unresolved-column-reference", diagnostics[0].Code);
        Assert.Contains("Ambiguous", diagnostics[0].Message);
        Assert.Contains("dbo.Users", diagnostics[0].Message);
        Assert.Contains("dbo.Orders", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_Wildcard_SkipsValidation()
    {
        const string sql = "SELECT * FROM dbo.Users;";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_NoSchema_ReturnsNoDiagnostics()
    {
        var context = RuleTestContext.CreateContext("SELECT u.Bad FROM dbo.Users AS u;");

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_TempTable_SkipsColumnValidation()
    {
        const string sql = "SELECT t.Col1 FROM #Temp AS t;";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_DerivedTable_SkipsColumnValidation()
    {
        const string sql = """
            SELECT d.Anything
            FROM (SELECT 1 AS Anything) AS d;
            """;
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_WhereClause_ValidatesColumns()
    {
        const string sql = "SELECT u.Id FROM dbo.Users AS u WHERE u.BadCol = 1;";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Contains("BadCol", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_CaseInsensitiveColumn_ReturnsNoDiagnostics()
    {
        const string sql = "SELECT u.ID, u.NAME FROM dbo.Users AS u;";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_UnqualifiedUniqueColumn_ReturnsNoDiagnostics()
    {
        const string sql = """
            SELECT Total
            FROM dbo.Users AS u
            INNER JOIN dbo.Orders AS o ON u.Id = o.UserId;
            """;
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_NoFromClause_SkipsValidation()
    {
        const string sql = "SELECT 1 AS Col;";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_SchemaQualifiedColumn_ResolvesCorrectTable()
    {
        const string sql = """
            SELECT sales.Orders.SalesTotal
            FROM dbo.Orders
            INNER JOIN sales.Orders ON dbo.Orders.Id = sales.Orders.Id;
            """;
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_OrderByMissingColumn_ReturnsDiagnostic()
    {
        const string sql = "SELECT u.Id FROM dbo.Users AS u ORDER BY u.BadCol;";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Contains("BadCol", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_OrderByValidColumn_ReturnsNoDiagnostics()
    {
        const string sql = "SELECT u.Id FROM dbo.Users AS u ORDER BY u.Name;";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_GroupByMissingColumn_ReturnsDiagnostic()
    {
        const string sql = "SELECT u.Name FROM dbo.Users AS u GROUP BY u.BadCol;";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Contains("BadCol", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_GroupByValidColumn_ReturnsNoDiagnostics()
    {
        const string sql = "SELECT u.Name FROM dbo.Users AS u GROUP BY u.Name;";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_OrderByUnqualifiedMissingColumn_ReturnsDiagnostic()
    {
        const string sql = "SELECT Id FROM dbo.Users ORDER BY BadCol;";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Contains("BadCol", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_GroupByUnqualifiedMissingColumn_ReturnsDiagnostic()
    {
        const string sql = "SELECT Name FROM dbo.Users GROUP BY BadCol;";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Contains("BadCol", diagnostics[0].Message);
    }
}
