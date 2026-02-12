using System.Collections.Frozen;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace TsqlRefine.Rules.Helpers.Analysis;

/// <summary>
/// Shared utilities for aggregate function detection in T-SQL analysis rules.
/// </summary>
public static class AggregateFunctionHelpers
{
    /// <summary>
    /// The set of T-SQL aggregate function names (case-insensitive).
    /// </summary>
    public static readonly FrozenSet<string> AggregateFunctions = FrozenSet.ToFrozenSet(
    [
        "COUNT", "SUM", "AVG", "MIN", "MAX",
        "COUNT_BIG", "STDEV", "STDEVP", "VAR", "VARP",
        "STRING_AGG", "GROUPING", "GROUPING_ID",
        "CHECKSUM_AGG", "APPROX_COUNT_DISTINCT"
    ], StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Checks whether a FunctionCall is an aggregate function.
    /// Window functions (with OVER clause) are treated as aggregates
    /// because their internal column references have independent scoping.
    /// </summary>
    public static bool IsAggregateFunction(FunctionCall func)
    {
        if (func.OverClause != null)
        {
            return true;
        }

        var name = func.FunctionName?.Value;
        return name != null && AggregateFunctions.Contains(name);
    }
}
