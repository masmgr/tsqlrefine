using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Helpers.Schema;

internal static class DmlWriteTargetHelpers
{
    internal static ResolvedTable? ResolveNamedTarget(ISchemaProvider schema, TableReference? target)
    {
        if (target is not NamedTableReference named)
        {
            return null;
        }

        var name = named.SchemaObject;
        var tableName = name.BaseIdentifier?.Value;
        return string.IsNullOrWhiteSpace(tableName) || AliasMapBuilder.IsTemporaryOrVariable(tableName)
            ? null
            : schema.ResolveTable(name.DatabaseIdentifier?.Value, name.SchemaIdentifier?.Value, tableName);
    }

    internal static ResolvedTable? ResolveUpdateTarget(ISchemaProvider schema, UpdateSpecification specification)
    {
        if (specification.Target is not NamedTableReference named)
        {
            return null;
        }

        var name = named.SchemaObject;
        var tableName = name.BaseIdentifier?.Value;
        if (string.IsNullOrWhiteSpace(tableName) || AliasMapBuilder.IsTemporaryOrVariable(tableName))
        {
            return null;
        }

        if (name.DatabaseIdentifier is not null || name.SchemaIdentifier is not null)
        {
            return schema.ResolveTable(name.DatabaseIdentifier?.Value, name.SchemaIdentifier?.Value, tableName);
        }

        if (specification.FromClause?.TableReferences is { Count: > 0 } tableReferences)
        {
            var aliases = AliasMapBuilder.Build(tableReferences, schema);
            if (aliases.TryResolve(tableName, out var resolved))
            {
                return resolved;
            }
        }

        return schema.ResolveTable(null, null, tableName);
    }

    internal static string? GetColumnName(ColumnReferenceExpression? column) =>
        column?.MultiPartIdentifier?.Identifiers.LastOrDefault()?.Value;

    internal static string GetTableKey(ResolvedTable table) =>
        $"{table.DatabaseName}\u001f{table.SchemaName}\u001f{table.TableName}";
}
