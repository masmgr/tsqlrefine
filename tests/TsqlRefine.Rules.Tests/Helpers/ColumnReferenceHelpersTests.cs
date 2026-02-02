using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.Rules.Helpers;
using Xunit;

namespace TsqlRefine.Rules.Tests.Helpers;

public sealed class ColumnReferenceHelpersTests
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

    private static ColumnReferenceExpression? GetFirstColumnReference(string sql)
    {
        var querySpec = GetQuerySpec(sql);
        var visitor = new ColumnFinder();
        querySpec.Accept(visitor);
        return visitor.Found;
    }

    private sealed class ColumnFinder : TSqlFragmentVisitor
    {
        public ColumnReferenceExpression? Found { get; private set; }

        public override void ExplicitVisit(ColumnReferenceExpression node)
        {
            Found ??= node;
            base.ExplicitVisit(node);
        }
    }

    #region GetTableQualifier Tests

    [Fact]
    public void GetTableQualifier_WithQualifiedColumn_ReturnsQualifier()
    {
        // Arrange
        var column = GetFirstColumnReference("SELECT t1.name FROM table1 t1");

        // Act
        var qualifier = ColumnReferenceHelpers.GetTableQualifier(column);

        // Assert
        Assert.Equal("t1", qualifier);
    }

    [Fact]
    public void GetTableQualifier_WithUnqualifiedColumn_ReturnsNull()
    {
        // Arrange
        var column = GetFirstColumnReference("SELECT name FROM table1");

        // Act
        var qualifier = ColumnReferenceHelpers.GetTableQualifier(column);

        // Assert
        Assert.Null(qualifier);
    }

    [Fact]
    public void GetTableQualifier_WithSchemaQualifiedColumn_ReturnsTableName()
    {
        // Arrange (schema.table.column)
        var column = GetFirstColumnReference("SELECT dbo.users.name FROM dbo.users");

        // Act
        var qualifier = ColumnReferenceHelpers.GetTableQualifier(column);

        // Assert - should return table name, not schema
        Assert.Equal("users", qualifier);
    }

    [Fact]
    public void GetTableQualifier_WithNull_ReturnsNull()
    {
        // Act
        var qualifier = ColumnReferenceHelpers.GetTableQualifier(null);

        // Assert
        Assert.Null(qualifier);
    }

    #endregion

    #region CollectTableQualifiers Tests

    [Fact]
    public void CollectTableQualifiers_WithMultipleQualifiedColumns_CollectsAll()
    {
        // Arrange
        var querySpec = GetQuerySpec("SELECT t1.a, t2.b FROM table1 t1 JOIN table2 t2 ON t1.id = t2.id");

        // Act
        var qualifiers = ColumnReferenceHelpers.CollectTableQualifiers(querySpec);

        // Assert
        Assert.Contains("t1", qualifiers);
        Assert.Contains("t2", qualifiers);
    }

    [Fact]
    public void CollectTableQualifiers_WithMixedColumns_CollectsOnlyQualified()
    {
        // Arrange
        var querySpec = GetQuerySpec("SELECT t1.a, b FROM table1 t1");

        // Act
        var qualifiers = ColumnReferenceHelpers.CollectTableQualifiers(querySpec);

        // Assert
        Assert.Single(qualifiers);
        Assert.Contains("t1", qualifiers);
    }

    [Fact]
    public void CollectTableQualifiers_IsCaseInsensitive()
    {
        // Arrange
        var querySpec = GetQuerySpec("SELECT T1.a FROM table1 t1");

        // Act
        var qualifiers = ColumnReferenceHelpers.CollectTableQualifiers(querySpec);

        // Assert
        Assert.Contains("t1", qualifiers); // lowercase lookup
        Assert.Contains("T1", qualifiers); // uppercase lookup
    }

    [Fact]
    public void CollectTableQualifiers_WithNull_ReturnsEmptySet()
    {
        // Act
        var qualifiers = ColumnReferenceHelpers.CollectTableQualifiers(null);

        // Assert
        Assert.Empty(qualifiers);
    }

    [Fact]
    public void CollectTableQualifiers_DoesNotDescendIntoSubqueries()
    {
        // Arrange - subquery has t2 reference but we should not collect it
        var querySpec = GetQuerySpec(@"
            SELECT t1.a
            FROM table1 t1
            WHERE t1.id IN (SELECT t2.id FROM table2 t2)");

        // Act - collect from main query only
        var qualifiers = ColumnReferenceHelpers.CollectTableQualifiers(querySpec.SelectElements[0]);

        // Assert - only t1 should be collected
        Assert.Single(qualifiers);
        Assert.Contains("t1", qualifiers);
    }

    #endregion

    #region AreColumnReferencesEqual Tests

    [Fact]
    public void AreColumnReferencesEqual_WithSameColumns_ReturnsTrue()
    {
        // Arrange
        var fragment = ParseSql("SELECT t1.a, t1.a FROM table1 t1");
        var visitor = new AllColumnsFinder();
        fragment.Accept(visitor);

        // Act
        var result = ColumnReferenceHelpers.AreColumnReferencesEqual(
            visitor.Found[0], visitor.Found[1]);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void AreColumnReferencesEqual_WithDifferentColumns_ReturnsFalse()
    {
        // Arrange
        var fragment = ParseSql("SELECT t1.a, t1.b FROM table1 t1");
        var visitor = new AllColumnsFinder();
        fragment.Accept(visitor);

        // Act
        var result = ColumnReferenceHelpers.AreColumnReferencesEqual(
            visitor.Found[0], visitor.Found[1]);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void AreColumnReferencesEqual_WithDifferentQualifiers_ReturnsFalse()
    {
        // Arrange
        var fragment = ParseSql("SELECT t1.a, t2.a FROM table1 t1, table2 t2");
        var visitor = new AllColumnsFinder();
        fragment.Accept(visitor);

        // Act
        var result = ColumnReferenceHelpers.AreColumnReferencesEqual(
            visitor.Found[0], visitor.Found[1]);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void AreColumnReferencesEqual_IsCaseInsensitive()
    {
        // Arrange
        var fragment = ParseSql("SELECT T1.A, t1.a FROM table1 t1");
        var visitor = new AllColumnsFinder();
        fragment.Accept(visitor);

        // Act
        var result = ColumnReferenceHelpers.AreColumnReferencesEqual(
            visitor.Found[0], visitor.Found[1]);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void AreColumnReferencesEqual_WithNull_ReturnsFalse()
    {
        // Arrange
        var column = GetFirstColumnReference("SELECT t1.a FROM table1 t1");

        // Act & Assert
        Assert.False(ColumnReferenceHelpers.AreColumnReferencesEqual(null, column));
        Assert.False(ColumnReferenceHelpers.AreColumnReferencesEqual(column, null));
        Assert.False(ColumnReferenceHelpers.AreColumnReferencesEqual(null, null));
    }

    private sealed class AllColumnsFinder : TSqlFragmentVisitor
    {
        public List<ColumnReferenceExpression> Found { get; } = new();

        public override void ExplicitVisit(ColumnReferenceExpression node)
        {
            Found.Add(node);
            base.ExplicitVisit(node);
        }
    }

    #endregion
}
