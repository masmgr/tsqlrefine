using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace TsqlRefine.Rules.Helpers.Visitors;

/// <summary>
/// Base visitor that tracks whether we're inside a predicate context
/// (WHERE, JOIN ON, or HAVING clauses).
/// </summary>
public abstract class PredicateAwareVisitorBase : DiagnosticVisitorBase
{
    /// <summary>
    /// Gets a value indicating whether the visitor is currently inside a predicate context.
    /// </summary>
    protected bool IsInPredicate { get; private set; }

    /// <summary>
    /// Visits a WHERE clause and sets the predicate context.
    /// </summary>
    public override void ExplicitVisit(WhereClause node)
    {
        IsInPredicate = true;
        base.ExplicitVisit(node);
        IsInPredicate = false;
    }

    /// <summary>
    /// Visits a qualified JOIN and sets the predicate context for the ON condition.
    /// </summary>
    public override void ExplicitVisit(QualifiedJoin node)
    {
        // Check JOIN ON conditions
        if (node.SearchCondition != null)
        {
            IsInPredicate = true;
            node.SearchCondition.Accept(this);
            IsInPredicate = false;
        }

        // Continue visiting other parts of the join
        if (node.FirstTableReference != null)
            node.FirstTableReference.Accept(this);
        if (node.SecondTableReference != null)
            node.SecondTableReference.Accept(this);
    }

    /// <summary>
    /// Visits a HAVING clause and sets the predicate context.
    /// </summary>
    public override void ExplicitVisit(HavingClause node)
    {
        IsInPredicate = true;
        base.ExplicitVisit(node);
        IsInPredicate = false;
    }
}
