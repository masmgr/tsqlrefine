using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace TsqlRefine.Rules.Helpers;

/// <summary>
/// A base visitor that blocks traversal at subquery scope boundaries.
/// Use this for visitors that collect column references within the current scope only.
/// </summary>
/// <remarks>
/// This visitor blocks traversal into:
/// - SelectStatement (nested SELECT)
/// - ScalarSubquery (subquery in SELECT list or expression)
/// - QueryDerivedTable (subquery in FROM clause)
///
/// This ensures column references from inner scopes are not mixed with outer scope analysis.
/// </remarks>
public abstract class ScopeBlockingVisitor : TSqlFragmentVisitor
{
    /// <summary>
    /// Stops traversal at nested SELECT statements - they have their own scope.
    /// </summary>
    public override void ExplicitVisit(SelectStatement node)
    {
        // Stop here - don't traverse into nested SELECT statements
    }

    /// <summary>
    /// Stops traversal at scalar subqueries - they have their own scope.
    /// </summary>
    public override void ExplicitVisit(ScalarSubquery node)
    {
        // Stop here - don't traverse into scalar subqueries
    }

    /// <summary>
    /// Stops traversal at query derived tables (subqueries in FROM) - they have their own scope.
    /// </summary>
    public override void ExplicitVisit(QueryDerivedTable node)
    {
        // Stop here - don't traverse into derived table subqueries
    }
}
