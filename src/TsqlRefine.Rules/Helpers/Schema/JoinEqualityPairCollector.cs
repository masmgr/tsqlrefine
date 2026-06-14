using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace TsqlRefine.Rules.Helpers.Schema;

/// <summary>
/// Extracts equality column pairs (left = right) from a JOIN ON condition.
/// </summary>
internal static class JoinEqualityPairCollector
{
    /// <summary>
    /// Extracts equality pairs from a boolean expression.
    /// When <paramref name="andOnly"/> is true (default), only AND-connected comparisons are collected.
    /// </summary>
    public static List<(ColumnReferenceExpression Left, ColumnReferenceExpression Right, BooleanComparisonExpression Node)> Extract(
        BooleanExpression? condition,
        bool andOnly = true)
    {
        var results = new List<(ColumnReferenceExpression, ColumnReferenceExpression, BooleanComparisonExpression)>();
        if (condition is not null)
        {
            Collect(condition, results, andOnly);
        }

        return results;
    }

    private static void Collect(
        BooleanExpression condition,
        List<(ColumnReferenceExpression, ColumnReferenceExpression, BooleanComparisonExpression)> results,
        bool andOnly)
    {
        switch (condition)
        {
            case BooleanComparisonExpression { ComparisonType: BooleanComparisonType.Equals } comparison
                when comparison.FirstExpression is ColumnReferenceExpression leftCol
                  && comparison.SecondExpression is ColumnReferenceExpression rightCol:
                results.Add((leftCol, rightCol, comparison));
                break;

            case BooleanBinaryExpression binary
                when !andOnly || binary.BinaryExpressionType == BooleanBinaryExpressionType.And:
                Collect(binary.FirstExpression, results, andOnly);
                Collect(binary.SecondExpression, results, andOnly);
                break;

            case BooleanParenthesisExpression paren:
                Collect(paren.Expression, results, andOnly);
                break;
        }
    }
}
