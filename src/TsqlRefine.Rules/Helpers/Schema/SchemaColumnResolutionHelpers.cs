using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Helpers.Schema;

/// <summary>Shared schema-backed column resolution for rules using an alias map.</summary>
internal static class SchemaColumnResolutionHelpers
{
    internal static ResolvedColumn? ResolveColumn(
        ISchemaProvider schema,
        ColumnReferenceExpression column,
        AliasMap aliasMap)
    {
        var identifiers = column.MultiPartIdentifier?.Identifiers;
        if (identifiers is null or { Count: 0 })
        {
            return null;
        }

        var columnName = identifiers[^1].Value;
        if (identifiers.Count >= 2)
        {
            return QualifierLookupKeyBuilder.TryResolve(aliasMap, identifiers, out var table) && table is not null
                ? schema.ResolveColumn(table, columnName)
                : null;
        }

        if (aliasMap.HasUnresolvableEntries)
        {
            return null;
        }

        ResolvedColumn? match = null;
        foreach (var table in aliasMap.AllTables)
        {
            if (schema.ResolveColumn(table, columnName) is { } resolved)
            {
                if (match is not null)
                {
                    return null;
                }

                match = resolved;
            }
        }

        return match;
    }
}
