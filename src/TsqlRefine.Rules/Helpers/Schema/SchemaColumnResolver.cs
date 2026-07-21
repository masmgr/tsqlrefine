using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Helpers.Schema;

/// <summary>
/// Encapsulates per-query column resolution caches and logic shared by schema-aware rules.
/// </summary>
internal sealed class SchemaColumnResolver
{
    private readonly ISchemaProvider _schema;
    private Dictionary<string, (ResolvedTable Table, string ColumnName)?>? _unqualifiedColumnResolutionCache;

    public SchemaColumnResolver(ISchemaProvider schema, AliasMap aliasMap)
    {
        _schema = schema;
        AliasMap = aliasMap;
    }

    public AliasMap AliasMap { get; }

    /// <summary>
    /// Resolves a column reference to its table and column name.
    /// </summary>
    public (ResolvedTable Table, string ColumnName)? ResolveColumnToTable(ColumnReferenceExpression colRef)
    {
        return ResolveColumnToTableCore(colRef);
    }

    /// <summary>
    /// Checks whether a column exists in the given table.
    /// </summary>
    public bool ColumnExists(ResolvedTable table, string columnName)
    {
        return _schema.ResolveColumn(table, columnName) is not null;
    }

    private (ResolvedTable Table, string ColumnName)? ResolveColumnToTableCore(ColumnReferenceExpression colRef)
    {
        if (colRef.ColumnType == ColumnType.Wildcard)
        {
            return null;
        }

        var identifiers = colRef.MultiPartIdentifier?.Identifiers;
        if (identifiers is null or { Count: 0 })
        {
            return null;
        }

        var columnName = identifiers[identifiers.Count - 1].Value;

        if (identifiers.Count >= 2)
        {
            if (QualifierLookupKeyBuilder.TryResolve(AliasMap, identifiers, out var resolved))
            {
                return resolved is null ? null : (resolved, columnName);
            }

            return null;
        }

        // An unqualified column may belong to an unverifiable source (CTE, derived
        // table, temp table, ...) — attribution to a resolved table would be a guess.
        if (AliasMap.HasUnresolvableEntries)
        {
            return null;
        }

        if (_unqualifiedColumnResolutionCache?.TryGetValue(columnName, out var unqualifiedCached) == true)
        {
            return unqualifiedCached;
        }

        (ResolvedTable Table, string ColumnName)? match = null;
        var matchCount = 0;
        foreach (var table in AliasMap.AllTables)
        {
            if (ColumnExists(table, columnName))
            {
                match = (table, columnName);
                matchCount++;
                if (matchCount > 1)
                {
                    (_unqualifiedColumnResolutionCache ??= new(StringComparer.OrdinalIgnoreCase))[columnName] = null;
                    return null;
                }
            }
        }

        (_unqualifiedColumnResolutionCache ??= new(StringComparer.OrdinalIgnoreCase))[columnName] = match;
        return match;
    }
}
