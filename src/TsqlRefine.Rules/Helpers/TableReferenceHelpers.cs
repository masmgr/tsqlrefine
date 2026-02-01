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

    /// <summary>
    /// Traverses JOIN conditions recursively and invokes an action for each QualifiedJoin's search condition.
    /// </summary>
    /// <param name="tableRef">The table reference to start from.</param>
    /// <param name="conditionAction">Action to invoke with the join and its search condition.</param>
    public static void TraverseJoinConditions(
        TableReference tableRef,
        Action<QualifiedJoin, BooleanExpression> conditionAction)
    {
        ArgumentNullException.ThrowIfNull(conditionAction);

        if (tableRef is QualifiedJoin qualifiedJoin)
        {
            if (qualifiedJoin.SearchCondition != null)
            {
                conditionAction(qualifiedJoin, qualifiedJoin.SearchCondition);
            }

            TraverseJoinConditions(qualifiedJoin.FirstTableReference, conditionAction);
            TraverseJoinConditions(qualifiedJoin.SecondTableReference, conditionAction);
        }
        else if (tableRef is JoinTableReference join)
        {
            TraverseJoinConditions(join.FirstTableReference, conditionAction);
            TraverseJoinConditions(join.SecondTableReference, conditionAction);
        }
    }

    /// <summary>
    /// Collects all QualifiedJoins of a specific type from a list of table references.
    /// </summary>
    /// <param name="tableRefs">The list of table references to search.</param>
    /// <param name="joinType">The type of join to collect.</param>
    /// <returns>An enumerable of matching QualifiedJoin nodes.</returns>
    public static IEnumerable<QualifiedJoin> CollectJoinsOfType(
        IList<TableReference> tableRefs,
        QualifiedJoinType joinType)
    {
        ArgumentNullException.ThrowIfNull(tableRefs);

        foreach (var tableRef in tableRefs)
        {
            foreach (var join in CollectJoinsOfTypeRecursive(tableRef, joinType))
            {
                yield return join;
            }
        }
    }

    private static IEnumerable<QualifiedJoin> CollectJoinsOfTypeRecursive(
        TableReference tableRef,
        QualifiedJoinType joinType)
    {
        if (tableRef is QualifiedJoin qualifiedJoin)
        {
            if (qualifiedJoin.QualifiedJoinType == joinType)
            {
                yield return qualifiedJoin;
            }

            foreach (var join in CollectJoinsOfTypeRecursive(qualifiedJoin.FirstTableReference, joinType))
            {
                yield return join;
            }

            foreach (var join in CollectJoinsOfTypeRecursive(qualifiedJoin.SecondTableReference, joinType))
            {
                yield return join;
            }
        }
        else if (tableRef is JoinTableReference join)
        {
            foreach (var j in CollectJoinsOfTypeRecursive(join.FirstTableReference, joinType))
            {
                yield return j;
            }

            foreach (var j in CollectJoinsOfTypeRecursive(join.SecondTableReference, joinType))
            {
                yield return j;
            }
        }
    }
}
