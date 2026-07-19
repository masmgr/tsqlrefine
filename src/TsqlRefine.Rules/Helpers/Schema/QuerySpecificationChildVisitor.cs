using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace TsqlRefine.Rules.Helpers.Schema;

/// <summary>
/// Visits every child of a <see cref="QuerySpecification"/> exactly once with the given visitor.
/// Schema-aware visitors that set up per-query state (alias maps, resolvers) on
/// <see cref="QuerySpecification"/> use this instead of <c>base.ExplicitVisit</c> so that
/// subqueries in every clause (SELECT list, WHERE, HAVING, ORDER BY, TOP, ...) are still
/// traversed while the FROM clause is not visited twice.
/// </summary>
internal static class QuerySpecificationChildVisitor
{
    public static void VisitChildren(TSqlFragmentVisitor visitor, QuerySpecification node)
    {
        if (node.SelectElements is not null)
        {
            foreach (var element in node.SelectElements)
            {
                element.Accept(visitor);
            }
        }

        node.WhereClause?.Accept(visitor);
        node.HavingClause?.Accept(visitor);
        node.OrderByClause?.Accept(visitor);
        node.GroupByClause?.Accept(visitor);
        node.TopRowFilter?.Accept(visitor);
        node.OffsetClause?.Accept(visitor);
        node.WindowClause?.Accept(visitor);
        node.ForClause?.Accept(visitor);
        node.FromClause?.Accept(visitor);
    }
}
