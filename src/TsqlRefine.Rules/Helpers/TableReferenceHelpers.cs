using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace TsqlRefine.Rules.Helpers;

/// <summary>
/// Helper utilities for working with table references in SQL queries.
/// </summary>
public static class TableReferenceHelpers
{
    /// <summary>
    /// Recursively collects all leaf table references from a list of table references.
    /// Handles JoinTableReference by traversing both sides.
    /// </summary>
    /// <param name="tableRefs">The list of table references to process.</param>
    /// <param name="collected">The collection to add leaf table references to.</param>
    public static void CollectTableReferences(IList<TableReference> tableRefs, ICollection<TableReference> collected)
    {
        ArgumentNullException.ThrowIfNull(tableRefs);
        ArgumentNullException.ThrowIfNull(collected);

        foreach (var tableRef in tableRefs)
        {
            if (tableRef is JoinTableReference join)
            {
                // Recursively collect from both sides of the JOIN
                CollectTableReferences(new[] { join.FirstTableReference }, collected);
                CollectTableReferences(new[] { join.SecondTableReference }, collected);
            }
            else
            {
                // This is a leaf table reference (NamedTableReference, QueryDerivedTable, etc.)
                collected.Add(tableRef);
            }
        }
    }

    /// <summary>
    /// Collects all declared table aliases/names from a list of table references.
    /// </summary>
    /// <param name="tableRefs">The list of table references to process.</param>
    /// <returns>A HashSet containing all aliases (case-insensitive).</returns>
    public static HashSet<string> CollectTableAliases(IList<TableReference> tableRefs)
    {
        ArgumentNullException.ThrowIfNull(tableRefs);

        var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        CollectTableAliasesCore(tableRefs, aliases);
        return aliases;
    }

    private static void CollectTableAliasesCore(IList<TableReference> tableRefs, HashSet<string> aliases)
    {
        foreach (var tableRef in tableRefs)
        {
            if (tableRef is JoinTableReference join)
            {
                // Recursively collect from both sides of the JOIN
                CollectTableAliasesCore(new[] { join.FirstTableReference }, aliases);
                CollectTableAliasesCore(new[] { join.SecondTableReference }, aliases);
            }
            else
            {
                var alias = GetAliasOrTableName(tableRef);
                if (alias != null)
                {
                    aliases.Add(alias);
                }
            }
        }
    }

    /// <summary>
    /// Gets the alias (if defined) or the base table name from a table reference.
    /// </summary>
    /// <param name="tableRef">The table reference to get the alias from.</param>
    /// <returns>The alias or table name, or null if not available.</returns>
    public static string? GetAliasOrTableName(TableReference tableRef)
    {
        return tableRef switch
        {
            NamedTableReference namedTable =>
                namedTable.Alias?.Value ?? namedTable.SchemaObject.BaseIdentifier.Value,
            QueryDerivedTable derivedTable =>
                derivedTable.Alias?.Value,
            _ => null
        };
    }
}
