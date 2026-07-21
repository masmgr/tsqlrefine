using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Helpers.Schema;

/// <summary>
/// Tracks columns that may be supplied by an enclosing DML target to a correlated subquery.
/// </summary>
internal sealed class DmlTargetColumnScopeManager(ISchemaProvider schema)
{
    private readonly Stack<TargetScope> _scopes = new();

    internal bool TryPush(UpdateSpecification? updateSpecification)
    {
        var targetScope = ResolveTargetScope(updateSpecification);
        if (targetScope is null)
        {
            return false;
        }

        _scopes.Push(targetScope);
        return true;
    }

    internal void Pop() => _scopes.Pop();

    internal bool CanResolve(string columnName)
    {
        foreach (var targetScope in _scopes)
        {
            if (targetScope.IsIndeterminate ||
                targetScope.Table is not null && schema.ResolveColumn(targetScope.Table, columnName) is not null)
            {
                return true;
            }
        }

        return false;
    }

    private TargetScope? ResolveTargetScope(UpdateSpecification? updateSpecification)
    {
        if (updateSpecification?.Target is VariableTableReference)
        {
            return TargetScope.Indeterminate;
        }

        if (updateSpecification?.Target is not NamedTableReference namedTarget ||
            namedTarget.SchemaObject.BaseIdentifier?.Value is not { } targetName)
        {
            return null;
        }

        if (AliasMapBuilder.IsTemporaryOrVariable(targetName))
        {
            return TargetScope.Indeterminate;
        }

        if (updateSpecification.FromClause?.TableReferences is { Count: > 0 } tableRefs)
        {
            var aliasMap = AliasMapBuilder.Build(tableRefs, schema);
            if (aliasMap.TryResolve(targetName, out var mappedTarget))
            {
                return mappedTarget is null
                    ? TargetScope.Indeterminate
                    : new TargetScope(mappedTarget, IsIndeterminate: false);
            }
        }

        var schemaObject = namedTarget.SchemaObject;
        var resolved = schema.ResolveTable(
            schemaObject.DatabaseIdentifier?.Value,
            schemaObject.SchemaIdentifier?.Value,
            targetName);
        return resolved is null ? null : new TargetScope(resolved, IsIndeterminate: false);
    }

    private sealed record TargetScope(ResolvedTable? Table, bool IsIndeterminate)
    {
        internal static TargetScope Indeterminate { get; } = new(null, IsIndeterminate: true);
    }
}
