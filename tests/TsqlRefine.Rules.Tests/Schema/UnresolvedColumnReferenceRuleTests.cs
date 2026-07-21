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

    [Fact]
    public void Analyze_CorrelatedSubqueryWithUnqualifiedOuterColumn_ReturnsNoDiagnostics()
    {
        const string sql = """
            SELECT u.Id
            FROM dbo.Users AS u
            WHERE EXISTS (
                SELECT 1
                FROM dbo.Orders AS o
                WHERE Email = N'test'
            );
            """;
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    // === Complex query scenarios ===

    [Fact]
    public void Analyze_DerivedTableJoinWithUnqualifiedDerivedColumn_ReturnsNoDiagnostics()
    {
        // OrderCount only exists in the derived table; it must not be reported
        // as missing just because dbo.Users does not contain it.
        const string sql = """
            SELECT u.Name, OrderCount
            FROM dbo.Users AS u
            INNER JOIN (
                SELECT o.UserId, COUNT(*) AS OrderCount
                FROM dbo.Orders AS o
                GROUP BY o.UserId
            ) AS agg ON agg.UserId = u.Id;
            """;
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_CteJoinWithUnqualifiedCteColumn_ReturnsNoDiagnostics()
    {
        const string sql = """
            WITH OrderTotals AS (
                SELECT o.UserId, SUM(o.Total) AS TotalAmount
                FROM dbo.Orders AS o
                GROUP BY o.UserId
            )
            SELECT u.Name, TotalAmount
            FROM dbo.Users AS u
            INNER JOIN OrderTotals AS t ON t.UserId = u.Id;
            """;
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_OrderBySelectAlias_ReturnsNoDiagnostics()
    {
        const string sql = """
            SELECT u.Name AS DisplayName
            FROM dbo.Users AS u
            ORDER BY DisplayName;
            """;
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_MissingSelectExpressionMatchingItsAlias_ReturnsDiagnostic()
    {
        const string sql = "SELECT Nonexistent AS Nonexistent FROM dbo.Users;";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        var diagnostic = Assert.Single(diagnostics);
        Assert.Contains("Nonexistent", diagnostic.Message);
    }

    [Fact]
    public void Analyze_SelectAliasReferencedInWhere_ReturnsDiagnostic()
    {
        const string sql = "SELECT u.Name AS BadCol FROM dbo.Users AS u WHERE BadCol = N'x';";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        var diagnostic = Assert.Single(diagnostics);
        Assert.Contains("BadCol", diagnostic.Message);
    }

    [Fact]
    public void Analyze_OrderBySelectAliasMatchingAmbiguousSourceColumn_ReturnsNoDiagnostics()
    {
        const string sql = """
            SELECT u.Name AS Id
            FROM dbo.Users AS u
            INNER JOIN dbo.Orders AS o ON u.Id = o.UserId
            ORDER BY Id;
            """;
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_UnknownTableJoinWithUnqualifiedColumn_ReturnsNoDiagnostics()
    {
        // dbo.LegacyCodes is not in the snapshot; its columns cannot be verified,
        // so unqualified references must not be reported.
        const string sql = """
            SELECT LegacyCode
            FROM dbo.Users AS u
            INNER JOIN dbo.LegacyCodes AS x ON x.UserId = u.Id;
            """;
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_TempTableJoinWithUnqualifiedColumn_ReturnsNoDiagnostics()
    {
        const string sql = """
            SELECT StagedValue
            FROM dbo.Users AS u
            INNER JOIN #Staging AS s ON s.UserId = u.Id;
            """;
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_PivotWithJoinedTable_ReturnsNoDiagnostics()
    {
        const string sql = """
            SELECT p.UserId, p.[1], p.[2]
            FROM (
                SELECT o.UserId, o.Id, o.Total
                FROM dbo.Orders AS o
            ) AS src
            PIVOT (SUM(Total) FOR Id IN ([1], [2])) AS p
            INNER JOIN dbo.Users AS u ON u.Id = p.UserId;
            """;
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_QualifiedMissingColumnWithDerivedTableInScope_ReturnsDiagnostic()
    {
        // Qualified references to resolved tables must still be validated even
        // when unresolvable sources are present in the same FROM clause.
        const string sql = """
            SELECT u.BadCol, d.Anything
            FROM dbo.Users AS u
            INNER JOIN (SELECT 1 AS Anything) AS d ON 1 = 1;
            """;
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Contains("BadCol", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_NestedDerivedTablesWithOuterReferences_ReturnsNoDiagnostics()
    {
        const string sql = """
            SELECT outerQuery.Name
            FROM (
                SELECT innerQuery.Name
                FROM (
                    SELECT u.Name
                    FROM dbo.Users AS u
                ) AS innerQuery
            ) AS outerQuery
            WHERE outerQuery.Name = N'test';
            """;
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_CrossApplyWithUnqualifiedApplyColumn_ReturnsNoDiagnostics()
    {
        const string sql = """
            SELECT u.Name, LatestTotal
            FROM dbo.Users AS u
            CROSS APPLY (
                SELECT TOP (1) o.Total AS LatestTotal
                FROM dbo.Orders AS o
                WHERE o.UserId = u.Id
                ORDER BY o.Id DESC
            ) AS latest;
            """;
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_CorrelatedSubqueryReferencingTempUpdateTarget_ReturnsNoDiagnostics()
    {
        const string sql = """
            CREATE TABLE #Work (OuterId int, Value int);

            UPDATE #Work
            SET Value = (
                SELECT TOP (1) o.Total
                FROM dbo.Orders AS o
                WHERE o.Id = OuterId
            );
            """;
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_SubqueryMissingFromPersistentUpdateTarget_ReturnsDiagnostic()
    {
        const string sql = """
            UPDATE dbo.Users
            SET Name = (
                SELECT TOP (1) MissingColumn
                FROM dbo.Orders
            );
            """;
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostic = Assert.Single(_rule.Analyze(context));

        Assert.Contains("MissingColumn", diagnostic.Message);
    }

    [Fact]
    public void Analyze_WidthInsensitiveDatabaseCollation_ResolvesDifferentWidthIdentifier()
    {
        var schema = new SchemaProvider(TestSchemaBuilder.Create()
            .WithDatabaseCollation("Japanese_CI_AS", 1041, comparisonStyle: 1 | 65536 | 131072)
            .AddView("dbo", "V_NIPPOU_KYUKA", view => view
                .AddColumn("日付の種類（承認済）", "nvarchar", maxLength: 100))
            .Build());
        var context = RuleTestContext.CreateContext(
            "SELECT v.[日付の種類(承認済)] FROM dbo.V_NIPPOU_KYUKA AS v;",
            schema);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }
}
