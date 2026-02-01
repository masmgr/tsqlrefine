using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.Rules.Helpers;
using Xunit;

namespace TsqlRefine.Rules.Tests.Helpers;

public sealed class ScopeBoundaryAwareVisitorTests
{
    private static TSqlFragment ParseSql(string sql)
    {
        var parser = new TSql160Parser(true);
        var fragment = parser.Parse(new System.IO.StringReader(sql), out var errors);
        return fragment;
    }

    [Fact]
    public void Visitor_DoesNotDescendIntoNestedSelectStatement()
    {
        // Arrange
        var fragment = ParseSql(@"
            SELECT outer_col
            FROM outer_table
            WHERE id IN (SELECT inner_col FROM inner_table)");

        var visitor = new ColumnCountingVisitor();

        // Act
        fragment.Accept(visitor);

        // Assert - should only see outer_col and id, not inner_col
        // (id is from WHERE clause, outer_col is from SELECT)
        Assert.DoesNotContain("inner_col", visitor.ColumnNames);
    }

    [Fact]
    public void Visitor_DoesNotDescendIntoScalarSubquery()
    {
        // Arrange
        var fragment = ParseSql(@"
            SELECT outer_col, (SELECT inner_col FROM inner_table) AS sub
            FROM outer_table");

        var visitor = new ColumnCountingVisitor();

        // Act
        fragment.Accept(visitor);

        // Assert - should only see outer_col, not inner_col
        Assert.DoesNotContain("inner_col", visitor.ColumnNames);
    }

    [Fact]
    public void Visitor_DoesNotDescendIntoQueryDerivedTable()
    {
        // Arrange
        var fragment = ParseSql(@"
            SELECT dt.col
            FROM (SELECT inner_col AS col FROM inner_table) dt");

        var visitor = new ColumnCountingVisitor();

        // Act
        fragment.Accept(visitor);

        // Assert - should only see dt.col, not inner_col
        Assert.DoesNotContain("inner_col", visitor.ColumnNames);
    }

    [Fact]
    public void Visitor_VisitsColumnsInCurrentScope()
    {
        // Arrange
        var fragment = ParseSql(@"
            SELECT t1.a, t2.b
            FROM table1 t1
            JOIN table2 t2 ON t1.id = t2.id
            WHERE t1.c > 0");

        var script = (TSqlScript)fragment;
        var batch = script.Batches[0];
        var selectStatement = (SelectStatement)batch.Statements[0];
        var querySpec = (QuerySpecification)selectStatement.QueryExpression;

        var visitor = new ColumnCountingVisitor();

        // Act - visit the query specification directly, not the full fragment
        querySpec.Accept(visitor);

        // Assert - should see all columns in current scope
        Assert.Contains("a", visitor.ColumnNames);
        Assert.Contains("b", visitor.ColumnNames);
        Assert.Contains("id", visitor.ColumnNames);
        Assert.Contains("c", visitor.ColumnNames);
    }

    private sealed class ColumnCountingVisitor : ScopeBoundaryAwareVisitor
    {
        public List<string> ColumnNames { get; } = new();

        public override void ExplicitVisit(ColumnReferenceExpression node)
        {
            if (node.MultiPartIdentifier?.Identifiers != null)
            {
                // Get the last identifier (column name)
                var columnName = node.MultiPartIdentifier.Identifiers[^1].Value;
                ColumnNames.Add(columnName);
            }

            base.ExplicitVisit(node);
        }
    }
}
