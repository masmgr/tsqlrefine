using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers.Schema;

namespace TsqlRefine.Rules.Rules.Schema;

/// <summary>
/// Detects UPDATE...FROM...JOIN statements where the joined table has a one-to-many relationship
/// with the target table, causing non-deterministic updates.
/// </summary>
public sealed class UpdateJoinCardinalityMismatchRule : SchemaAwareVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "update-join-cardinality-mismatch",
        Description: "Detects UPDATE...FROM...JOIN where the join may produce multiple rows per target row, causing non-deterministic updates.",
        Category: "Schema",
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new UpdateJoinCardinalityVisitor(context.Schema!);

    private sealed class UpdateJoinCardinalityVisitor(ISchemaProvider schema) : DiagnosticVisitorBase
    {
        private SchemaColumnResolver? _resolver;
        private ResolvedTable? _currentTarget;
        private string? _currentUnresolvedTargetQualifier;
        private string? _currentTargetDisplayName;

        public override void ExplicitVisit(UpdateStatement node)
        {
            var updateSpec = node.UpdateSpecification;
            if (updateSpec?.FromClause?.TableReferences is not { Count: > 0 } tableRefs)
            {
                base.ExplicitVisit(node);
                return;
            }

            var targetName = GetTargetName(updateSpec.Target);
            if (targetName is null)
            {
                base.ExplicitVisit(node);
                return;
            }

            var aliasMap = AliasMapBuilder.Build(tableRefs, schema);
            var resolvedTarget = updateSpec.Target is NamedTableReference namedTarget
                ? ResolveUpdateTarget(
                    targetName,
                    namedTarget.SchemaObject.DatabaseIdentifier?.Value,
                    namedTarget.SchemaObject.SchemaIdentifier?.Value,
                    aliasMap)
                : null;
            var unresolvedTarget = resolvedTarget is null
                ? ResolveTemporaryOrVariableTarget(targetName, tableRefs)
                : null;

            if (resolvedTarget is null && unresolvedTarget is null)
            {
                base.ExplicitVisit(node);
                return;
            }

            var previousResolver = _resolver;
            var previousTarget = _currentTarget;
            var previousUnresolvedTargetQualifier = _currentUnresolvedTargetQualifier;
            var previousTargetDisplayName = _currentTargetDisplayName;

            _resolver = new SchemaColumnResolver(schema, aliasMap);
            _currentTarget = resolvedTarget;
            _currentUnresolvedTargetQualifier = unresolvedTarget?.Qualifier;
            _currentTargetDisplayName = unresolvedTarget?.DisplayName
                ?? (resolvedTarget is null ? targetName : $"{resolvedTarget.SchemaName}.{resolvedTarget.TableName}");

            foreach (var tableRef in tableRefs)
            {
                TraverseJoins(tableRef);
            }

            _resolver = previousResolver;
            _currentTarget = previousTarget;
            _currentUnresolvedTargetQualifier = previousUnresolvedTargetQualifier;
            _currentTargetDisplayName = previousTargetDisplayName;

            base.ExplicitVisit(node);
        }

        private void TraverseJoins(TableReference tableRef)
        {
            switch (tableRef)
            {
                case QualifiedJoin qualifiedJoin:
                    if (qualifiedJoin.SearchCondition is not null)
                    {
                        CheckJoinCardinality(qualifiedJoin);
                    }

                    TraverseJoins(qualifiedJoin.FirstTableReference);
                    TraverseJoins(qualifiedJoin.SecondTableReference);
                    break;

                case JoinTableReference join:
                    TraverseJoins(join.FirstTableReference);
                    TraverseJoins(join.SecondTableReference);
                    break;

                case JoinParenthesisTableReference joinParen when joinParen.Join is not null:
                    TraverseJoins(joinParen.Join);
                    break;
            }
        }

        private void CheckJoinCardinality(QualifiedJoin joinNode)
        {
            var pairs = JoinEqualityPairCollector.Extract(joinNode.SearchCondition);
            if (pairs.Count == 0)
            {
                return;
            }

            var targetColumns = new List<string>();
            var joinedColumns = new List<string>();
            ResolvedTable? joinedTable = null;

            foreach (var (leftCol, rightCol, _) in pairs)
            {
                var leftResolved = _resolver!.ResolveColumnToTable(leftCol);
                var rightResolved = _resolver.ResolveColumnToTable(rightCol);

                if (_currentTarget is null)
                {
                    CollectUnresolvedTargetPair(
                        leftCol, rightCol, leftResolved, rightResolved,
                        targetColumns, joinedColumns, ref joinedTable);
                    continue;
                }

                if (leftResolved is null || rightResolved is null)
                {
                    continue;
                }

                var (leftTable, leftColName) = leftResolved.Value;
                var (rightTable, rightColName) = rightResolved.Value;

                if (ResolvedTableComparers.TablesAreEqual(leftTable, _currentTarget!))
                {
                    if (joinedTable is not null && !ResolvedTableComparers.TablesAreEqual(rightTable, joinedTable))
                    {
                        continue;
                    }

                    targetColumns.Add(leftColName);
                    joinedColumns.Add(rightColName);
                    joinedTable ??= rightTable;
                }
                else if (ResolvedTableComparers.TablesAreEqual(rightTable, _currentTarget!))
                {
                    if (joinedTable is not null && !ResolvedTableComparers.TablesAreEqual(leftTable, joinedTable))
                    {
                        continue;
                    }

                    targetColumns.Add(rightColName);
                    joinedColumns.Add(leftColName);
                    joinedTable ??= leftTable;
                }
            }

            if (targetColumns.Count == 0 || joinedTable is null)
            {
                return;
            }

            var canMatchMultipleRows = _currentTarget is null
                ? !schema.IsUniqueColumnSet(joinedTable, joinedColumns)
                : schema.EstimateJoinCardinality(
                    _currentTarget, targetColumns,
                    joinedTable, joinedColumns) is JoinCardinality.OneToMany or JoinCardinality.ManyToMany;

            if (canMatchMultipleRows)
            {
                AddDiagnostic(
                    fragment: joinNode.SearchCondition,
                    message: $"UPDATE may be non-deterministic: join to '{joinedTable.SchemaName}.{joinedTable.TableName}' can match multiple rows per '{_currentTargetDisplayName}' row (join columns on '{joinedTable.TableName}' are not unique).",
                    code: "update-join-cardinality-mismatch",
                    category: "Schema",
                    fixable: false);
            }
        }

        private void CollectUnresolvedTargetPair(
            ColumnReferenceExpression leftColumn,
            ColumnReferenceExpression rightColumn,
            (ResolvedTable Table, string ColumnName)? leftResolved,
            (ResolvedTable Table, string ColumnName)? rightResolved,
            ICollection<string> targetColumns,
            ICollection<string> joinedColumns,
            ref ResolvedTable? joinedTable)
        {
            if (_currentUnresolvedTargetQualifier is null)
            {
                return;
            }

            if (IsQualifiedBy(leftColumn, _currentUnresolvedTargetQualifier) && rightResolved is not null)
            {
                TryAddPair(leftColumn, rightResolved.Value, targetColumns, joinedColumns, ref joinedTable);
            }
            else if (IsQualifiedBy(rightColumn, _currentUnresolvedTargetQualifier) && leftResolved is not null)
            {
                TryAddPair(rightColumn, leftResolved.Value, targetColumns, joinedColumns, ref joinedTable);
            }
        }

        private static void TryAddPair(
            ColumnReferenceExpression targetColumn,
            (ResolvedTable Table, string ColumnName) joined,
            ICollection<string> targetColumns,
            ICollection<string> joinedColumns,
            ref ResolvedTable? joinedTable)
        {
            if (joinedTable is not null && !ResolvedTableComparers.TablesAreEqual(joined.Table, joinedTable))
            {
                return;
            }

            var targetColumnName = targetColumn.MultiPartIdentifier?.Identifiers.LastOrDefault()?.Value;
            if (targetColumnName is null)
            {
                return;
            }

            targetColumns.Add(targetColumnName);
            joinedColumns.Add(joined.ColumnName);
            joinedTable ??= joined.Table;
        }

        private static bool IsQualifiedBy(ColumnReferenceExpression column, string qualifier)
        {
            var identifiers = column.MultiPartIdentifier?.Identifiers;
            return identifiers is { Count: >= 2 }
                && string.Equals(identifiers[^2].Value, qualifier, StringComparison.OrdinalIgnoreCase);
        }

        private static string? GetTargetName(TableReference? target) => target switch
        {
            NamedTableReference named => named.SchemaObject.BaseIdentifier?.Value,
            VariableTableReference variable => variable.Variable?.Name,
            _ => null,
        };

        private static UnresolvedUpdateTarget? ResolveTemporaryOrVariableTarget(
            string targetName,
            IList<TableReference> tableRefs)
        {
            var leaves = new List<TableReference>();
            TableReferenceHelpers.CollectTableReferences(tableRefs, leaves);

            foreach (var leaf in leaves)
            {
                if (leaf is NamedTableReference named &&
                    named.SchemaObject.BaseIdentifier?.Value is { } baseName &&
                    AliasMapBuilder.IsTemporaryOrVariable(baseName))
                {
                    var qualifier = named.Alias?.Value ?? baseName;
                    if (string.Equals(targetName, qualifier, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(targetName, baseName, StringComparison.OrdinalIgnoreCase))
                    {
                        return new UnresolvedUpdateTarget(qualifier, baseName);
                    }
                }

                if (leaf is VariableTableReference variable && variable.Variable?.Name is { } variableName)
                {
                    var qualifier = variable.Alias?.Value ?? variableName;
                    if (string.Equals(targetName, qualifier, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(targetName, variableName, StringComparison.OrdinalIgnoreCase))
                    {
                        return new UnresolvedUpdateTarget(qualifier, variableName);
                    }
                }
            }

            return null;
        }

        private ResolvedTable? ResolveUpdateTarget(
            string tableName,
            string? dbName,
            string? schemaName,
            AliasMap aliasMap)
        {
            if (dbName is not null || schemaName is not null)
            {
                return schema.ResolveTable(dbName, schemaName, tableName);
            }

            if (aliasMap.TryResolve(tableName, out var mapped))
            {
                return mapped;
            }

            return schema.ResolveTable(null, null, tableName);
        }

        private sealed record UnresolvedUpdateTarget(string Qualifier, string DisplayName);
    }
}
