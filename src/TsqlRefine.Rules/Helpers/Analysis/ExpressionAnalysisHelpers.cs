using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace TsqlRefine.Rules.Helpers.Analysis;

/// <summary>
/// Helper utilities for analyzing SQL expressions.
/// </summary>
public static class ExpressionAnalysisHelpers
{
    /// <summary>
    /// Checks if the expression contains a column reference (direct or nested).
    /// Handles CAST, CONVERT, FunctionCall, BinaryExpression, and ParenthesisExpression.
    /// </summary>
    /// <param name="expression">The expression to check.</param>
    /// <returns>True if the expression contains a column reference, false otherwise.</returns>
    public static bool ContainsColumnReference(ScalarExpression? expression)
    {
        if (expression == null)
            return false;

        // Direct column reference
        if (expression is ColumnReferenceExpression)
            return true;

        // Check nested expressions
        if (expression is CastCall castCall)
            return ContainsColumnReference(castCall.Parameter);

        if (expression is ConvertCall convertCall)
            return ContainsColumnReference(convertCall.Parameter);

        if (expression is FunctionCall functionCall && functionCall.Parameters != null)
        {
            for (var i = 0; i < functionCall.Parameters.Count; i++)
            {
                var parameter = functionCall.Parameters[i];
                if (DatePartHelper.IsDatePartLiteralParameter(functionCall, i, parameter))
                {
                    continue;
                }

                if (ContainsColumnReference(parameter))
                {
                    return true;
                }
            }
            return false;
        }

        if (expression is BinaryExpression binaryExpr)
            return ContainsColumnReference(binaryExpr.FirstExpression) ||
                   ContainsColumnReference(binaryExpr.SecondExpression);

        if (expression is ParenthesisExpression parenExpr)
            return ContainsColumnReference(parenExpr.Expression);

        return false;
    }
}
