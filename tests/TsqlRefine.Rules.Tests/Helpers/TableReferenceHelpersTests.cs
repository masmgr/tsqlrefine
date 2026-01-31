using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.Rules.Helpers;
using Xunit;

namespace TsqlRefine.Rules.Tests.Helpers;

public sealed class TableReferenceHelpersTests
{
    private static TSqlFragment ParseSql(string sql)
    {
        var parser = new TSql160Parser(true);
        var fragment = parser.Parse(new System.IO.StringReader(sql), out var errors);
        return fragment;
    }

    private static QuerySpecification GetQuerySpec(string sql)
    {
        var fragment = ParseSql(sql);
        var script = (TSqlScript)fragment;
        var batch = script.Batches[0];
        var selectStatement = (SelectStatement)batch.Statements[0];
        return (QuerySpecification)selectStatement.QueryExpression;
    }

    [Fact]
    public void CollectTableReferences_WithSingleTable_CollectsOneReference()
    {
        // Arrange
        var querySpec = GetQuerySpec("SELECT * FROM users");
        var collected = new List<TableReference>();

        // Act
        TableReferenceHelpers.CollectTableReferences(querySpec.FromClause.TableReferences, collected);

        // Assert
        Assert.Single(collected);
    }

    [Fact]
    public void CollectTableReferences_WithJoin_CollectsBothTables()
    {
        // Arrange
        var querySpec = GetQuerySpec("SELECT * FROM users u INNER JOIN orders o ON u.id = o.user_id");
        var collected = new List<TableReference>();

        // Act
        TableReferenceHelpers.CollectTableReferences(querySpec.FromClause.TableReferences, collected);

        // Assert
        Assert.Equal(2, collected.Count);
    }

    [Fact]
    public void CollectTableReferences_WithMultipleJoins_CollectsAllTables()
    {
        // Arrange
        var querySpec = GetQuerySpec(@"
            SELECT * FROM users u
            INNER JOIN orders o ON u.id = o.user_id
            LEFT JOIN products p ON o.product_id = p.id");
        var collected = new List<TableReference>();

        // Act
        TableReferenceHelpers.CollectTableReferences(querySpec.FromClause.TableReferences, collected);

        // Assert
        Assert.Equal(3, collected.Count);
    }

    [Fact]
    public void CollectTableReferences_WithNullList_ThrowsArgumentNullException()
    {
        // Arrange
        var collected = new List<TableReference>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            TableReferenceHelpers.CollectTableReferences(null!, collected));
    }

    [Fact]
    public void CollectTableReferences_WithNullCollection_ThrowsArgumentNullException()
    {
        // Arrange
        var querySpec = GetQuerySpec("SELECT * FROM users");

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            TableReferenceHelpers.CollectTableReferences(querySpec.FromClause.TableReferences, null!));
    }

    [Fact]
    public void CollectTableAliases_WithSingleTable_CollectsTableName()
    {
        // Arrange
        var querySpec = GetQuerySpec("SELECT * FROM users");

        // Act
        var aliases = TableReferenceHelpers.CollectTableAliases(querySpec.FromClause.TableReferences);

        // Assert
        Assert.Single(aliases);
        Assert.Contains("users", aliases);
    }

    [Fact]
    public void CollectTableAliases_WithAlias_CollectsAlias()
    {
        // Arrange
        var querySpec = GetQuerySpec("SELECT * FROM users u");

        // Act
        var aliases = TableReferenceHelpers.CollectTableAliases(querySpec.FromClause.TableReferences);

        // Assert
        Assert.Single(aliases);
        Assert.Contains("u", aliases);
    }

    [Fact]
    public void CollectTableAliases_WithJoin_CollectsAllAliases()
    {
        // Arrange
        var querySpec = GetQuerySpec("SELECT * FROM users u INNER JOIN orders o ON u.id = o.user_id");

        // Act
        var aliases = TableReferenceHelpers.CollectTableAliases(querySpec.FromClause.TableReferences);

        // Assert
        Assert.Equal(2, aliases.Count);
        Assert.Contains("u", aliases);
        Assert.Contains("o", aliases);
    }

    [Fact]
    public void CollectTableAliases_IsCaseInsensitive()
    {
        // Arrange
        var querySpec = GetQuerySpec("SELECT * FROM users U");

        // Act
        var aliases = TableReferenceHelpers.CollectTableAliases(querySpec.FromClause.TableReferences);

        // Assert
        Assert.Contains("u", aliases); // lowercase lookup
        Assert.Contains("U", aliases); // uppercase lookup
    }

    [Fact]
    public void CollectTableAliases_WithNullList_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            TableReferenceHelpers.CollectTableAliases(null!));
    }

    [Fact]
    public void GetAliasOrTableName_WithNamedTableNoAlias_ReturnsTableName()
    {
        // Arrange
        var querySpec = GetQuerySpec("SELECT * FROM users");
        var collected = new List<TableReference>();
        TableReferenceHelpers.CollectTableReferences(querySpec.FromClause.TableReferences, collected);
        var tableRef = collected[0];

        // Act
        var name = TableReferenceHelpers.GetAliasOrTableName(tableRef);

        // Assert
        Assert.Equal("users", name);
    }

    [Fact]
    public void GetAliasOrTableName_WithNamedTableAndAlias_ReturnsAlias()
    {
        // Arrange
        var querySpec = GetQuerySpec("SELECT * FROM users u");
        var collected = new List<TableReference>();
        TableReferenceHelpers.CollectTableReferences(querySpec.FromClause.TableReferences, collected);
        var tableRef = collected[0];

        // Act
        var name = TableReferenceHelpers.GetAliasOrTableName(tableRef);

        // Assert
        Assert.Equal("u", name);
    }

    [Fact]
    public void GetAliasOrTableName_WithDerivedTable_ReturnsAlias()
    {
        // Arrange
        var querySpec = GetQuerySpec("SELECT * FROM (SELECT 1 AS id) sub");
        var collected = new List<TableReference>();
        TableReferenceHelpers.CollectTableReferences(querySpec.FromClause.TableReferences, collected);
        var tableRef = collected[0];

        // Act
        var name = TableReferenceHelpers.GetAliasOrTableName(tableRef);

        // Assert
        Assert.Equal("sub", name);
    }

    [Fact]
    public void GetAliasOrTableName_WithSchemaQualifiedTable_ReturnsTableName()
    {
        // Arrange
        var querySpec = GetQuerySpec("SELECT * FROM dbo.users");
        var collected = new List<TableReference>();
        TableReferenceHelpers.CollectTableReferences(querySpec.FromClause.TableReferences, collected);
        var tableRef = collected[0];

        // Act
        var name = TableReferenceHelpers.GetAliasOrTableName(tableRef);

        // Assert
        Assert.Equal("users", name);
    }
}
