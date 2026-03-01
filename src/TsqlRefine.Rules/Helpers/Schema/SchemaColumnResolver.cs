using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Helpers.Schema;

/// <summary>
/// Encapsulates per-query column resolution caches and logic shared by schema-aware rules.
/// </summary>
internal sealed class SchemaColumnResolver
{
    private readonly ISchemaProvider _schema;
    private readonly Dictionary<ColumnReferenceExpression, (ResolvedTable Table, string ColumnName)?> _resolvedColumnCache;
    private readonly Dictionary<(ResolvedTable Table, string ColumnName), bool> _columnExistsCache;
    private readonly Dictionary<string, (ResolvedTable Table, string ColumnName)?> _unqualifiedColumnResolutionCache;

    public SchemaColumnResolver(ISchemaProvider schema, AliasMap aliasMap)
    {
        _schema = schema;
        AliasMap = aliasMap;
        _resolvedColumnCache = new Dictionary<ColumnReferenceExpression, (ResolvedTable Table, string ColumnName)?>(
            ReferenceEqualityComparer.Instance);
        _columnExistsCache = new Dictionary<(ResolvedTable Table, string ColumnName), bool>(
            ResolvedTableComparers.TableColumnKeyComparer.Instance);
        _unqualifiedColumnResolutionCache = new Dictionary<string, (ResolvedTable Table, string ColumnName)?>(
            StringComparer.OrdinalIgnoreCase);
    }

    public AliasMap AliasMap { get; }

    /// <summary>
    /// Resolves a column reference to its table and column name, with caching.
    /// </summary>
    public (ResolvedTable Table, string ColumnName)? ResolveColumnToTable(ColumnReferenceExpression colRef)
    {
        if (_resolvedColumnCache.TryGetValue(colRef, out var cached))
        {
            return cached;
        }

        var resolved = ResolveColumnToTableCore(colRef);
        _resolvedColumnCache[colRef] = resolved;
        return resolved;
    }

    /// <summary>
    /// Checks whether a column exists in the given table, with caching.
    /// </summary>
    public bool ColumnExists(ResolvedTable table, string columnName)
    {
        if (_columnExistsCache.TryGetValue((table, columnName), out var exists))
        {
            return exists;
        }

        exists = _schema.ResolveColumn(table, columnName) is not null;
        _columnExistsCache[(table, columnName)] = exists;
        return exists;
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

        if (_unqualifiedColumnResolutionCache.TryGetValue(columnName, out var unqualifiedCached))
        {
            return unqualifiedCached;
        }

        foreach (var table in AliasMap.AllTables)
        {
            if (ColumnExists(table, columnName))
            {
                var result = (table, columnName);
                _unqualifiedColumnResolutionCache[columnName] = result;
                return result;
            }
        }

        _unqualifiedColumnResolutionCache[columnName] = null;
        return null;
    }
}
