using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace TsqlRefine.Rules.Helpers.Analysis;

/// <summary>
/// Provides traversal helpers for query-expression branches.
/// </summary>
public static class QueryExpressionAnalysisHelpers
{
    public static IEnumerable<QuerySpecification> EnumerateQuerySpecifications(
        QueryExpression queryExpression)
    {
        ArgumentNullException.ThrowIfNull(queryExpression);

        switch (queryExpression)
        {
            case QuerySpecification querySpecification:
                yield return querySpecification;
                break;
            case BinaryQueryExpression binaryQueryExpression:
                foreach (var query in EnumerateQuerySpecifications(binaryQueryExpression.FirstQueryExpression))
                {
                    yield return query;
                }

                foreach (var query in EnumerateQuerySpecifications(binaryQueryExpression.SecondQueryExpression))
                {
                    yield return query;
                }
                break;
            case QueryParenthesisExpression parenthesisExpression:
                foreach (var query in EnumerateQuerySpecifications(parenthesisExpression.QueryExpression))
                {
                    yield return query;
                }
                break;
        }
    }
}
