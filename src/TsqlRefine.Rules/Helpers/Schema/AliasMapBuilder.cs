using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Helpers.Schema;

/// <summary>
/// Maps table aliases/names to resolved tables for schema-aware analysis.
/// Entries with a null <see cref="ResolvedTable"/> represent unresolvable references
/// (CTEs, derived tables, temp tables) that should be skipped during column validation.
/// </summary>
public sealed class AliasMap
{
    private readonly Dictionary<string, ResolvedTable?> _map;
    private readonly List<ResolvedTable> _allTables;

    internal AliasMap(Dictionary<string, ResolvedTable?> map, List<ResolvedTable> allTables)
    {
        _map = map;
        _allTables = allTables;
    }

    /// <summary>
    /// Resolves an alias or table name to a <see cref="ResolvedTable"/>.
    /// Returns null if the alias is registered but unresolvable (CTE, derived table).
    /// Throws if the alias is not found at all.
    /// </summary>
    /// <param name="aliasOrTableName">The alias or table name to resolve.</param>
    /// <param name="resolved">The resolved table, or null if unresolvable.</param>
    /// <returns>True if the alias was found in the map (even if unresolvable), false otherwise.</returns>
    public bool TryResolve(string aliasOrTableName, out ResolvedTable? resolved)
    {
        return _map.TryGetValue(aliasOrTableName, out resolved);
    }

    /// <summary>
    /// Gets all successfully resolved tables (excludes unresolvable entries).
    /// </summary>
    public IReadOnlyList<ResolvedTable> AllTables => _allTables;
}

/// <summary>
/// Builds an <see cref="AliasMap"/> from FROM clause table references.
/// </summary>
public static class AliasMapBuilder
{
    /// <summary>
    /// Builds an alias map from a list of table references.
    /// </summary>
    /// <param name="tableRefs">The FROM clause table references.</param>
    /// <param name="schema">The schema provider for resolving table names.</param>
    /// <returns>An alias map mapping aliases/names to resolved tables.</returns>
    public static AliasMap Build(IList<TableReference> tableRefs, ISchemaProvider schema)
    {
        ArgumentNullException.ThrowIfNull(tableRefs);
        ArgumentNullException.ThrowIfNull(schema);

        var map = new Dictionary<string, ResolvedTable?>(StringComparer.OrdinalIgnoreCase);
        var allTables = new List<ResolvedTable>();

        foreach (var tableRef in tableRefs)
        {
            ProcessTableReference(tableRef, schema, map, allTables);
        }

        return new AliasMap(map, allTables);
    }

    /// <summary>
    /// Builds an alias map for CTE names, registering them as unresolvable.
    /// </summary>
    /// <param name="cteNames">The CTE names to register.</param>
    /// <param name="existing">An existing alias map to extend, or null.</param>
    /// <returns>A new alias map with CTE names registered as unresolvable.</returns>
    public static AliasMap WithCteNames(IEnumerable<string> cteNames, AliasMap? existing = null)
    {
        var map = new Dictionary<string, ResolvedTable?>(StringComparer.OrdinalIgnoreCase);
        var allTables = new List<ResolvedTable>();

        if (existing is not null)
        {
            foreach (var table in existing.AllTables)
            {
                allTables.Add(table);
            }
        }

        foreach (var cteName in cteNames)
        {
            map.TryAdd(cteName, null); // null = unresolvable
        }

        return new AliasMap(map, allTables);
    }

    private static void ProcessTableReference(
        TableReference tableRef,
        ISchemaProvider schema,
        Dictionary<string, ResolvedTable?> map,
        List<ResolvedTable> allTables)
    {
        switch (tableRef)
        {
            case JoinTableReference join:
                ProcessTableReference(join.FirstTableReference, schema, map, allTables);
                ProcessTableReference(join.SecondTableReference, schema, map, allTables);
                return;

            case JoinParenthesisTableReference joinParenthesis when joinParenthesis.Join is not null:
                ProcessTableReference(joinParenthesis.Join, schema, map, allTables);
                return;
        }

        if (tableRef is NamedTableReference namedTable)
        {
            var schemaObject = namedTable.SchemaObject;
            var tableName = schemaObject.BaseIdentifier?.Value;
            if (tableName is null)
            {
                return;
            }

            // Skip temp tables and table variables
            if (tableName.StartsWith('#') || tableName.StartsWith('@'))
            {
                var alias = namedTable.Alias?.Value ?? tableName;
                map.TryAdd(alias, null); // unresolvable
                return;
            }

            var schemaName = schemaObject.SchemaIdentifier?.Value;
            var dbName = schemaObject.DatabaseIdentifier?.Value;

            var resolved = schema.ResolveTable(dbName, schemaName, tableName);
            AddLookupKeys(
                map,
                tableName,
                schemaName,
                dbName,
                namedTable.Alias?.Value,
                resolved);

            if (resolved is not null)
            {
                allTables.Add(resolved);
            }
        }
        else
        {
            // Derived tables, function tables, etc. — register alias as unresolvable
            var alias = TableReferenceHelpers.GetAliasOrTableName(tableRef);
            if (alias is not null)
            {
                map.TryAdd(alias, null);
            }
        }
    }

    private static void AddLookupKeys(
        Dictionary<string, ResolvedTable?> map,
        string tableName,
        string? schemaName,
        string? dbName,
        string? alias,
        ResolvedTable? resolved)
    {
        if (!string.IsNullOrWhiteSpace(alias))
        {
            // When an alias exists, column qualifiers must use the alias.
            map.TryAdd(alias, resolved);
            return;
        }

        map.TryAdd(tableName, resolved);

        if (!string.IsNullOrWhiteSpace(schemaName))
        {
            map.TryAdd($"{schemaName}.{tableName}", resolved);
        }

        if (!string.IsNullOrWhiteSpace(dbName) && !string.IsNullOrWhiteSpace(schemaName))
        {
            map.TryAdd($"{dbName}.{schemaName}.{tableName}", resolved);
        }
    }
}
