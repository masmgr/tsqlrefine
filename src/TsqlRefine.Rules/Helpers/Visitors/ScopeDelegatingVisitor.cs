using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace TsqlRefine.Rules.Helpers.Visitors;

/// <summary>
/// A base visitor that delegates subquery processing to a callback instead of stopping traversal.
/// Use this for visitors that need to hand off subquery processing to a parent scope manager.
/// </summary>
/// <remarks>
/// Extends <see cref="ScopeBlockingVisitor"/> but instead of stopping at scope boundaries,
/// it invokes a delegate to process the subquery with proper scope tracking.
/// This is essential for rules that need to support correlated subqueries where inner queries
/// can reference aliases from outer scopes.
/// </remarks>
public abstract class ScopeDelegatingVisitor : ScopeBlockingVisitor
{
    /// <summary>
    /// Called when a subquery scope boundary is encountered.
    /// Derived classes should implement this to delegate subquery processing to the parent scope manager.
    /// </summary>
    /// <param name="queryExpression">The query expression to process, or null if not available.</param>
    protected abstract void ProcessSubquery(QueryExpression? queryExpression);

    /// <summary>
    /// Delegates nested SELECT statements to the scope manager.
    /// </summary>
    public override void ExplicitVisit(SelectStatement node)
    {
        ProcessSubquery(node.QueryExpression);
    }

    /// <summary>
    /// Delegates scalar subqueries to the scope manager.
    /// </summary>
    public override void ExplicitVisit(ScalarSubquery node)
    {
        ProcessSubquery(node.QueryExpression);
    }

    /// <summary>
    /// Delegates query derived tables (subqueries in FROM clause) to the scope manager.
    /// </summary>
    public override void ExplicitVisit(QueryDerivedTable node)
    {
        ProcessSubquery(node.QueryExpression);
    }
}
