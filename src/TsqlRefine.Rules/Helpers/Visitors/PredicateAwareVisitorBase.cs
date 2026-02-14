using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace TsqlRefine.Rules.Helpers.Visitors;

/// <summary>
/// Base visitor that tracks whether we're inside a predicate context
/// (WHERE, JOIN ON, or HAVING clauses).
/// </summary>
public abstract class PredicateAwareVisitorBase : DiagnosticVisitorBase
{
    private int _predicateDepth;

    /// <summary>
    /// Gets a value indicating whether the visitor is currently inside a predicate context.
    /// </summary>
    protected bool IsInPredicate => _predicateDepth > 0;

    /// <summary>
    /// Visits a WHERE clause and sets the predicate context.
    /// </summary>
    public override void ExplicitVisit(WhereClause node)
    {
        _predicateDepth++;
        try
        {
            base.ExplicitVisit(node);
        }
        finally
        {
            _predicateDepth--;
        }
    }

    /// <summary>
    /// Visits a qualified JOIN and sets the predicate context for the ON condition.
    /// </summary>
    public override void ExplicitVisit(QualifiedJoin node)
    {
        // Check JOIN ON conditions
        if (node.SearchCondition != null)
        {
            _predicateDepth++;
            try
            {
                node.SearchCondition.Accept(this);
            }
            finally
            {
                _predicateDepth--;
            }
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
        _predicateDepth++;
        try
        {
            base.ExplicitVisit(node);
        }
        finally
        {
            _predicateDepth--;
        }
    }
}
