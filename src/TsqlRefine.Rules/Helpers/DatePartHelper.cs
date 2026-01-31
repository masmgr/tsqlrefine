using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace TsqlRefine.Rules.Helpers;

/// <summary>
/// Helper utilities for identifying and working with T-SQL date/time functions
/// that use datepart literals (e.g., DATEADD, DATEDIFF, DATEPART, DATENAME).
/// </summary>
/// <remarks>
/// These functions have a special first parameter that is a datepart literal
/// (like 'year', 'month', 'day') which is parsed as an identifier/column reference
/// by ScriptDom, not as a string literal. This helper identifies such parameters
/// to avoid false positives in rules that check for unqualified column references.
/// </remarks>
public static class DatePartHelper
{
    private static readonly HashSet<string> DatePartFunctions = new(StringComparer.OrdinalIgnoreCase)
    {
        "DATEADD",
        "DATEDIFF",
        "DATEPART",
        "DATENAME"
    };

    /// <summary>
    /// Determines whether the given function call is a date part function
    /// (DATEADD, DATEDIFF, DATEPART, or DATENAME).
    /// </summary>
    /// <param name="func">The function call to check.</param>
    /// <returns>True if the function is a date part function; otherwise, false.</returns>
    public static bool IsDatePartFunction(FunctionCall func)
    {
        var name = func.FunctionName?.Value;
        return name != null && DatePartFunctions.Contains(name);
    }

    /// <summary>
    /// Determines whether the given parameter at the specified index is a datepart literal
    /// in a date part function call.
    /// </summary>
    /// <param name="func">The function call containing the parameter.</param>
    /// <param name="parameterIndex">The zero-based index of the parameter.</param>
    /// <param name="param">The parameter expression to check.</param>
    /// <returns>
    /// True if this is the first parameter of a date part function and it appears
    /// as a single-part column reference (indicating a datepart literal like 'year');
    /// otherwise, false.
    /// </returns>
    /// <remarks>
    /// ScriptDom parses datepart literals (e.g., DATEADD(year, 1, @date)) as
    /// ColumnReferenceExpression with a single identifier. This method helps
    /// distinguish such literals from actual column references.
    /// </remarks>
    public static bool IsDatePartLiteralParameter(FunctionCall func, int parameterIndex, ScalarExpression param)
    {
        if (parameterIndex != 0)
        {
            return false;
        }

        if (!IsDatePartFunction(func))
        {
            return false;
        }

        if (param is ColumnReferenceExpression colRef)
        {
            return colRef.MultiPartIdentifier != null && colRef.MultiPartIdentifier.Count == 1;
        }

        return false;
    }
}
