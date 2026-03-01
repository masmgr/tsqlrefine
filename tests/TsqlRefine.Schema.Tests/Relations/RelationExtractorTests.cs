using TsqlRefine.Schema.Relations;

namespace TsqlRefine.Schema.Tests.Relations;

public sealed class RelationExtractorTests
{
    private static List<RawJoinInfo> ExtractFromSql(string sql)
    {
        var fragment = SqlParser.Parse(sql, 150);
        Assert.NotNull(fragment);
        return RelationExtractor.Extract(fragment!, "test.sql");
    }

    [Fact]
    public void Extract_SimpleInnerJoin_ReturnsOneJoin()
    {
        var joins = ExtractFromSql(
            "SELECT * FROM dbo.Users u INNER JOIN dbo.Orders o ON u.Id = o.UserId");

        Assert.Single(joins);
        Assert.Equal("dbo", joins[0].LeftSchema);
        Assert.Equal("Users", joins[0].LeftTable);
        Assert.Equal("dbo", joins[0].RightSchema);
        Assert.Equal("Orders", joins[0].RightTable);
        Assert.Equal("INNER", joins[0].JoinType);
        Assert.Single(joins[0].ColumnPairs);
        Assert.Equal("Id", joins[0].ColumnPairs[0].LeftColumn);
        Assert.Equal("UserId", joins[0].ColumnPairs[0].RightColumn);
    }

    [Fact]
    public void Extract_LeftOuterJoin_ReturnsCorrectJoinType()
    {
        var joins = ExtractFromSql(
            "SELECT * FROM dbo.Users u LEFT OUTER JOIN dbo.Orders o ON u.Id = o.UserId");

        Assert.Single(joins);
        Assert.Equal("LEFT", joins[0].JoinType);
    }

    [Fact]
    public void Extract_RightOuterJoin_ReturnsCorrectJoinType()
    {
        var joins = ExtractFromSql(
            "SELECT * FROM dbo.Users u RIGHT JOIN dbo.Orders o ON u.Id = o.UserId");

        Assert.Single(joins);
        Assert.Equal("RIGHT", joins[0].JoinType);
    }

    [Fact]
    public void Extract_FullOuterJoin_ReturnsCorrectJoinType()
    {
        var joins = ExtractFromSql(
            "SELECT * FROM dbo.Users u FULL OUTER JOIN dbo.Orders o ON u.Id = o.UserId");

        Assert.Single(joins);
        Assert.Equal("FULL", joins[0].JoinType);
    }

    [Fact]
    public void Extract_MultipleJoins_ReturnsMultiple()
    {
        var joins = ExtractFromSql("""
            SELECT *
            FROM dbo.Users u
            INNER JOIN dbo.Orders o ON u.Id = o.UserId
            INNER JOIN dbo.Products p ON o.ProductId = p.Id
            """);

        Assert.Equal(2, joins.Count);
    }

    [Fact]
    public void Extract_MultiColumnOn_ReturnsMultipleColumnPairs()
    {
        var joins = ExtractFromSql(
            "SELECT * FROM dbo.A a INNER JOIN dbo.B b ON a.Id = b.AId AND a.Code = b.Code");

        Assert.Single(joins);
        Assert.Equal(2, joins[0].ColumnPairs.Count);
        Assert.Contains(joins[0].ColumnPairs, cp => cp.LeftColumn == "Id" && cp.RightColumn == "AId");
        Assert.Contains(joins[0].ColumnPairs, cp => cp.LeftColumn == "Code" && cp.RightColumn == "Code");
    }

    [Fact]
    public void Extract_CrossJoin_ReturnsNoColumnPairs()
    {
        var joins = ExtractFromSql(
            "SELECT * FROM dbo.Users u CROSS JOIN dbo.Roles r");

        Assert.Single(joins);
        Assert.Equal("CROSS", joins[0].JoinType);
        Assert.Empty(joins[0].ColumnPairs);
    }

    [Fact]
    public void Extract_SelfJoin_ReturnsSameTable()
    {
        var joins = ExtractFromSql(
            "SELECT * FROM dbo.Employees e1 INNER JOIN dbo.Employees e2 ON e1.ManagerId = e2.Id");

        Assert.Single(joins);
        Assert.Equal("Employees", joins[0].LeftTable);
        Assert.Equal("Employees", joins[0].RightTable);
    }

    [Fact]
    public void Extract_SubqueryTable_Skips()
    {
        var joins = ExtractFromSql(
            "SELECT * FROM dbo.Users u INNER JOIN (SELECT UserId FROM dbo.Orders) sub ON u.Id = sub.UserId");

        // The subquery is not a NamedTableReference, so it should be skipped
        Assert.Empty(joins);
    }

    [Fact]
    public void Extract_NoJoins_ReturnsEmpty()
    {
        var joins = ExtractFromSql("SELECT * FROM dbo.Users");

        Assert.Empty(joins);
    }

    [Fact]
    public void Extract_AliasedTables_ResolvesRealNames()
    {
        var joins = ExtractFromSql(
            "SELECT * FROM dbo.Users AS u INNER JOIN dbo.Orders AS o ON u.Id = o.UserId");

        Assert.Single(joins);
        Assert.Equal("Users", joins[0].LeftTable);
        Assert.Equal("Orders", joins[0].RightTable);
        Assert.Equal("Id", joins[0].ColumnPairs[0].LeftColumn);
        Assert.Equal("UserId", joins[0].ColumnPairs[0].RightColumn);
    }

    [Fact]
    public void Extract_UnqualifiedSchema_DefaultsToDbo()
    {
        var joins = ExtractFromSql(
            "SELECT * FROM Users u INNER JOIN Orders o ON u.Id = o.UserId");

        Assert.Single(joins);
        Assert.Equal("dbo", joins[0].LeftSchema);
        Assert.Equal("dbo", joins[0].RightSchema);
    }

    [Fact]
    public void Extract_QualifiedSchema_PreservesSchema()
    {
        var joins = ExtractFromSql(
            "SELECT * FROM sales.Orders o INNER JOIN hr.Employees e ON o.SalesPersonId = e.Id");

        Assert.Single(joins);
        Assert.Equal("sales", joins[0].LeftSchema);
        Assert.Equal("Orders", joins[0].LeftTable);
        Assert.Equal("hr", joins[0].RightSchema);
        Assert.Equal("Employees", joins[0].RightTable);
    }

    [Fact]
    public void Extract_SwappedColumnOrder_NormalizesToLeftRight()
    {
        // ON clause has right.col = left.col order
        var joins = ExtractFromSql(
            "SELECT * FROM dbo.Users u INNER JOIN dbo.Orders o ON o.UserId = u.Id");

        Assert.Single(joins);
        // Should normalize: left=Users column, right=Orders column
        Assert.Equal("Id", joins[0].ColumnPairs[0].LeftColumn);
        Assert.Equal("UserId", joins[0].ColumnPairs[0].RightColumn);
    }

    [Fact]
    public void Extract_ParenthesizedCondition_ExtractsColumnPairs()
    {
        var joins = ExtractFromSql(
            "SELECT * FROM dbo.A a INNER JOIN dbo.B b ON (a.Id = b.AId AND a.Code = b.Code)");

        Assert.Single(joins);
        Assert.Equal(2, joins[0].ColumnPairs.Count);
    }

    [Fact]
    public void Extract_MultipleStatements_ExtractsAll()
    {
        var joins = ExtractFromSql("""
            SELECT * FROM dbo.A a INNER JOIN dbo.B b ON a.Id = b.AId;
            SELECT * FROM dbo.C c LEFT JOIN dbo.D d ON c.Id = d.CId;
            """);

        Assert.Equal(2, joins.Count);
    }

    [Fact]
    public void Extract_SourceFileIsSet()
    {
        var fragment = SqlParser.Parse(
            "SELECT * FROM dbo.A a INNER JOIN dbo.B b ON a.Id = b.AId", 150);
        var joins = RelationExtractor.Extract(fragment!, "my/query.sql");

        Assert.Single(joins);
        Assert.Equal("my/query.sql", joins[0].SourceFile);
    }
}
