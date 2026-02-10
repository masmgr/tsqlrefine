using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace TsqlRefine.Rules.Helpers.Scope;

/// <summary>
/// Helpers for extracting and analyzing column names from SELECT element lists.
/// </summary>
public static class SelectColumnHelpers
{
    /// <summary>
    /// Extracts the output column name from a SELECT element.
    /// Returns the explicit alias if present, otherwise the last identifier of a column reference.
    /// Returns null for expressions without a deterministic name (e.g., SELECT *, function calls without alias).
    /// </summary>
    public static string? GetOutputColumnName(SelectElement element)
    {
        if (element is not SelectScalarExpression scalar)
        {
            return null;
        }

        // Prefer explicit alias
        if (!string.IsNullOrWhiteSpace(scalar.ColumnName?.Value))
        {
            return scalar.ColumnName.Value;
        }

        // Extract from column reference
        if (scalar.Expression is ColumnReferenceExpression colRef)
        {
            return GetLastIdentifier(colRef.MultiPartIdentifier);
        }

        return null;
    }

    /// <summary>
    /// Finds duplicate output column names in a SELECT element list.
    /// Returns each duplicate element with the duplicated column name.
    /// </summary>
    public static IEnumerable<(SelectElement Element, string Name)> FindDuplicateColumns(IList<SelectElement> selectElements)
    {
        var seen = new Dictionary<string, SelectElement>(StringComparer.OrdinalIgnoreCase);

        foreach (var element in selectElements)
        {
            var name = GetOutputColumnName(element);
            if (name == null)
            {
                continue;
            }

            if (seen.ContainsKey(name))
            {
                yield return (element, name);
            }
            else
            {
                seen[name] = element;
            }
        }
    }

    private static string? GetLastIdentifier(MultiPartIdentifier? identifier)
    {
        if (identifier == null || identifier.Identifiers.Count == 0)
        {
            return null;
        }

        return identifier.Identifiers[^1].Value;
    }
}
