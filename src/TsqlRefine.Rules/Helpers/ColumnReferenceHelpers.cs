using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace TsqlRefine.Rules.Helpers;

/// <summary>
/// Helper utilities for working with column references in SQL queries.
/// </summary>
public static class ColumnReferenceHelpers
{
    /// <summary>
    /// Gets the table qualifier (alias or table name) from a column reference.
    /// Returns the table/alias part from multi-part identifiers:
    /// - "t1.column" → "t1" (index 0)
    /// - "schema.table.column" → "table" (index 1)
    /// - "server.schema.table.column" → "table" (index 2)
    /// Formula: table is at index (count - 2)
    /// </summary>
    /// <param name="columnRef">The column reference expression.</param>
    /// <returns>The table qualifier, or null if no qualifier is present.</returns>
    public static string? GetTableQualifier(ColumnReferenceExpression? columnRef)
    {
        var count = columnRef?.MultiPartIdentifier?.Identifiers?.Count ?? 0;
        if (count > 1)
        {
            // Table/alias is always at index (count - 2)
            // 2 parts: table.column -> index 0
            // 3 parts: schema.table.column -> index 1
            // 4 parts: server.schema.table.column -> index 2
            return columnRef!.MultiPartIdentifier!.Identifiers[count - 2].Value;
        }

        return null;
    }

    /// <summary>
    /// Collects all table qualifiers from column references within a fragment.
    /// Does not descend into subqueries (respects scope boundaries).
    /// </summary>
    /// <param name="fragment">The SQL fragment to collect from.</param>
    /// <returns>A case-insensitive set of table qualifiers.</returns>
    public static HashSet<string> CollectTableQualifiers(TSqlFragment? fragment)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (fragment == null)
        {
            return result;
        }

        var visitor = new TableQualifierCollector(result);
        fragment.Accept(visitor);

        return result;
    }

    /// <summary>
    /// Checks if two column references refer to the same column (case-insensitive).
    /// </summary>
    /// <param name="first">The first column reference.</param>
    /// <param name="second">The second column reference.</param>
    /// <returns>True if both references have identical identifiers.</returns>
    public static bool AreColumnReferencesEqual(
        ColumnReferenceExpression? first,
        ColumnReferenceExpression? second)
    {
        var firstIdentifiers = first?.MultiPartIdentifier?.Identifiers;
        var secondIdentifiers = second?.MultiPartIdentifier?.Identifiers;

        if (firstIdentifiers == null || secondIdentifiers == null)
        {
            return false;
        }

        if (firstIdentifiers.Count != secondIdentifiers.Count)
        {
            return false;
        }

        for (int i = 0; i < firstIdentifiers.Count; i++)
        {
            if (!string.Equals(
                firstIdentifiers[i].Value,
                secondIdentifiers[i].Value,
                StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Internal visitor that collects table qualifiers from column references.
    /// </summary>
    private sealed class TableQualifierCollector : ScopeBoundaryAwareVisitor
    {
        private readonly HashSet<string> _qualifiers;

        public TableQualifierCollector(HashSet<string> qualifiers)
        {
            _qualifiers = qualifiers;
        }

        public override void ExplicitVisit(ColumnReferenceExpression node)
        {
            var qualifier = GetTableQualifier(node);
            if (qualifier != null)
            {
                _qualifiers.Add(qualifier);
            }

            base.ExplicitVisit(node);
        }
    }
}
