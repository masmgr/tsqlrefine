using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace TsqlRefine.Rules.Tests.Helpers;

public sealed class PredicateAwareVisitorBaseTests
{
    private static TSqlFragment ParseSql(string sql)
    {
        var parser = new TSql160Parser(true);
        var fragment = parser.Parse(new System.IO.StringReader(sql), out var errors);
        return fragment;
    }

    private sealed class TestPredicateVisitor : PredicateAwareVisitorBase
    {
        public List<(string Context, bool IsInPredicate)> Visits { get; } = new();

        public override void ExplicitVisit(ColumnReferenceExpression node)
        {
            var columnName = node.MultiPartIdentifier?.Identifiers.LastOrDefault()?.Value ?? "unknown";
            Visits.Add((columnName, IsInPredicate));
            base.ExplicitVisit(node);
        }
    }

    [Fact]
    public void IsInPredicate_InSelectList_IsFalse()
    {
        // Arrange
        var fragment = ParseSql("SELECT name FROM users");
        var visitor = new TestPredicateVisitor();

        // Act
        fragment.Accept(visitor);

        // Assert
        Assert.Contains(visitor.Visits, v => v.Context == "name" && !v.IsInPredicate);
    }

    [Fact]
    public void IsInPredicate_InWhereClause_IsTrue()
    {
        // Arrange
        var fragment = ParseSql("SELECT 1 FROM users WHERE name = 'test'");
        var visitor = new TestPredicateVisitor();

        // Act
        fragment.Accept(visitor);

        // Assert
        Assert.Contains(visitor.Visits, v => v.Context == "name" && v.IsInPredicate);
    }

    [Fact]
    public void IsInPredicate_InJoinCondition_IsTrue()
    {
        // Arrange
        var fragment = ParseSql("SELECT 1 FROM users u INNER JOIN orders o ON u.id = o.user_id");
        var visitor = new TestPredicateVisitor();

        // Act
        fragment.Accept(visitor);

        // Assert
        // id and user_id should be in predicate context
        Assert.Contains(visitor.Visits, v => v.Context == "id" && v.IsInPredicate);
        Assert.Contains(visitor.Visits, v => v.Context == "user_id" && v.IsInPredicate);
    }

    [Fact]
    public void IsInPredicate_InHavingClause_IsTrue()
    {
        // Arrange
        var fragment = ParseSql("SELECT status, COUNT(*) FROM orders GROUP BY status HAVING COUNT(*) > 5");
        var visitor = new TestPredicateVisitor();

        // Act
        fragment.Accept(visitor);

        // Assert
        // status in SELECT list should not be in predicate
        Assert.Contains(visitor.Visits, v => v.Context == "status" && !v.IsInPredicate);
    }

    [Fact]
    public void IsInPredicate_AfterWhereClause_IsFalseAgain()
    {
        // Arrange
        var fragment = ParseSql("SELECT col1, col2 FROM users WHERE id = 1 ORDER BY name");
        var visitor = new TestPredicateVisitor();

        // Act
        fragment.Accept(visitor);

        // Assert
        // col1, col2, name should not be in predicate, id should be
        Assert.Contains(visitor.Visits, v => v.Context == "col1" && !v.IsInPredicate);
        Assert.Contains(visitor.Visits, v => v.Context == "col2" && !v.IsInPredicate);
        Assert.Contains(visitor.Visits, v => v.Context == "id" && v.IsInPredicate);
        Assert.Contains(visitor.Visits, v => v.Context == "name" && !v.IsInPredicate);
    }

    [Fact]
    public void IsInPredicate_WithMultipleJoins_TracksCorrectly()
    {
        // Arrange
        var fragment = ParseSql(@"
            SELECT 1
            FROM users u
            INNER JOIN orders o ON u.id = o.user_id
            LEFT JOIN products p ON o.product_id = p.id");
        var visitor = new TestPredicateVisitor();

        // Act
        fragment.Accept(visitor);

        // Assert - all join conditions should be in predicate
        Assert.Contains(visitor.Visits, v => v.Context == "user_id" && v.IsInPredicate);
        Assert.Contains(visitor.Visits, v => v.Context == "product_id" && v.IsInPredicate);
    }

    [Fact]
    public void IsInPredicate_WithWhereAndJoin_BothAreInPredicate()
    {
        // Arrange
        var fragment = ParseSql(@"
            SELECT 1
            FROM users u
            INNER JOIN orders o ON u.id = o.user_id
            WHERE u.status = 'active'");
        var visitor = new TestPredicateVisitor();

        // Act
        fragment.Accept(visitor);

        // Assert
        Assert.Contains(visitor.Visits, v => v.Context == "user_id" && v.IsInPredicate);
        Assert.Contains(visitor.Visits, v => v.Context == "status" && v.IsInPredicate);
    }
}
