using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace TsqlRefine.Rules.Helpers.Analysis;

/// <summary>
/// Proves when a NOT IN subquery cannot produce a NULL value from its selected expression.
/// </summary>
public static class NotInNullabilityAnalysisHelpers
{
    public static bool IsSubqueryResultProvablyNonNull(ScalarSubquery subquery)
    {
        ArgumentNullException.ThrowIfNull(subquery);

        if (subquery.QueryExpression is not QuerySpecification querySpec ||
            querySpec.SelectElements is not [SelectScalarExpression { Expression: { } selectedExpression }])
        {
            return false;
        }

        return IsExpressionProvablyNonNull(selectedExpression) ||
               HasConjunctiveIsNotNullFilter(
                   querySpec.WhereClause?.SearchCondition,
                   selectedExpression);
    }

    private static bool IsExpressionProvablyNonNull(ScalarExpression expression)
    {
        expression = UnwrapParentheses(expression);
        return expression switch
        {
            Literal and not NullLiteral => true,
            CastCall cast => IsExpressionProvablyNonNull(cast.Parameter),
            ConvertCall convert => IsExpressionProvablyNonNull(convert.Parameter),
            CoalesceExpression coalesce => coalesce.Expressions.Any(IsExpressionProvablyNonNull),
            FunctionCall function when IsFunction(function, "ISNULL") =>
                function.Parameters.Count >= 2 &&
                (IsExpressionProvablyNonNull(function.Parameters[0]) ||
                 IsExpressionProvablyNonNull(function.Parameters[1])),
            FunctionCall function when IsFunction(function, "COUNT") || IsFunction(function, "COUNT_BIG") => true,
            _ => false
        };
    }

    private static bool HasConjunctiveIsNotNullFilter(
        BooleanExpression? condition,
        ScalarExpression selectedExpression)
    {
        return condition switch
        {
            BooleanIsNullExpression { IsNot: true } isNotNull =>
                GroupByColumnAnalysisHelpers.AreEquivalentExpressions(
                    selectedExpression,
                    isNotNull.Expression),
            BooleanBinaryExpression { BinaryExpressionType: BooleanBinaryExpressionType.And } binary =>
                HasConjunctiveIsNotNullFilter(binary.FirstExpression, selectedExpression) ||
                HasConjunctiveIsNotNullFilter(binary.SecondExpression, selectedExpression),
            BooleanParenthesisExpression parenthesis =>
                HasConjunctiveIsNotNullFilter(parenthesis.Expression, selectedExpression),
            _ => false
        };
    }

    private static ScalarExpression UnwrapParentheses(ScalarExpression expression)
    {
        while (expression is ParenthesisExpression parenthesis)
        {
            expression = parenthesis.Expression;
        }

        return expression;
    }

    private static bool IsFunction(FunctionCall function, string name) =>
        function.CallTarget is null &&
        string.Equals(function.FunctionName.Value, name, StringComparison.OrdinalIgnoreCase);
}
