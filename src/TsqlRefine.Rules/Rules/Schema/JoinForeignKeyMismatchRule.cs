using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers.Schema;

namespace TsqlRefine.Rules.Rules.Schema;

/// <summary>
/// Detects JOINs where the ON columns match a foreign key relationship but the joined table
/// differs from the FK target, indicating a likely accidental join to the wrong table.
/// </summary>
public sealed class JoinForeignKeyMismatchRule : SchemaAwareVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "join-foreign-key-mismatch",
        Description: "Detects JOINs where the ON columns match a foreign key relationship but the joined table differs from the FK target.",
        Category: "Schema",
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new JoinForeignKeyMismatchVisitor(context.Schema!);

    private sealed class JoinForeignKeyMismatchVisitor(ISchemaProvider schema) : DiagnosticVisitorBase
    {
        private AliasMap? _currentAliasMap;

        public override void ExplicitVisit(QuerySpecification node)
        {
            if (node.FromClause?.TableReferences is { Count: > 0 } tableRefs)
            {
                var previousMap = _currentAliasMap;
                _currentAliasMap = AliasMapBuilder.Build(tableRefs, schema);

                node.FromClause.Accept(this);

                _currentAliasMap = previousMap;
                return;
            }

            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(QualifiedJoin node)
        {
            if (_currentAliasMap is not null && node.SearchCondition is not null)
            {
                CheckJoinForeignKeyMismatch(node.SearchCondition);
            }

            base.ExplicitVisit(node);
        }

        private void CheckJoinForeignKeyMismatch(BooleanExpression condition)
        {
            var pairs = ExtractEqualityPairs(condition);

            foreach (var (leftCol, rightCol, comparison) in pairs)
            {
                var leftResolved = ResolveColumnToTable(leftCol);
                var rightResolved = ResolveColumnToTable(rightCol);

                if (leftResolved is null || rightResolved is null)
                {
                    continue;
                }

                var (leftTable, leftColumnName) = leftResolved.Value;
                var (rightTable, rightColumnName) = rightResolved.Value;

                CheckForeignKeyOnColumn(leftTable, leftColumnName, rightTable, rightColumnName, comparison);
                CheckForeignKeyOnColumn(rightTable, rightColumnName, leftTable, leftColumnName, comparison);
            }
        }

        private static List<(ColumnReferenceExpression Left, ColumnReferenceExpression Right, BooleanComparisonExpression Node)> ExtractEqualityPairs(
            BooleanExpression condition)
        {
            var results = new List<(ColumnReferenceExpression, ColumnReferenceExpression, BooleanComparisonExpression)>();
            CollectEqualityPairs(condition, results);
            return results;
        }

        private static void CollectEqualityPairs(
            BooleanExpression condition,
            List<(ColumnReferenceExpression, ColumnReferenceExpression, BooleanComparisonExpression)> results)
        {
            switch (condition)
            {
                case BooleanComparisonExpression { ComparisonType: BooleanComparisonType.Equals } comparison
                    when comparison.FirstExpression is ColumnReferenceExpression leftCol
                      && comparison.SecondExpression is ColumnReferenceExpression rightCol:
                    results.Add((leftCol, rightCol, comparison));
                    break;

                case BooleanBinaryExpression binary:
                    CollectEqualityPairs(binary.FirstExpression, results);
                    CollectEqualityPairs(binary.SecondExpression, results);
                    break;

                case BooleanParenthesisExpression paren:
                    CollectEqualityPairs(paren.Expression, results);
                    break;
            }
        }

        private (ResolvedTable Table, string ColumnName)? ResolveColumnToTable(ColumnReferenceExpression colRef)
        {
            if (_currentAliasMap is null || colRef.ColumnType == ColumnType.Wildcard)
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
                foreach (var key in BuildQualifierLookupKeys(identifiers))
                {
                    if (_currentAliasMap.TryResolve(key, out var resolved))
                    {
                        return resolved is null ? null : (resolved, columnName);
                    }
                }

                return null;
            }

            foreach (var table in _currentAliasMap.AllTables)
            {
                if (schema.ResolveColumn(table, columnName) is not null)
                {
                    return (table, columnName);
                }
            }

            return null;
        }

        private static IEnumerable<string> BuildQualifierLookupKeys(IList<Identifier> identifiers)
        {
            var qualifierCount = identifiers.Count - 1;
            if (qualifierCount <= 0)
            {
                yield break;
            }

            var parts = new string[qualifierCount];
            for (var i = 0; i < qualifierCount; i++)
            {
                parts[i] = identifiers[i].Value;
            }

            if (parts.Length == 1)
            {
                yield return parts[0];
                yield break;
            }

            yield return string.Join(".", parts);

            if (parts.Length >= 2)
            {
                yield return $"{parts[^2]}.{parts[^1]}";
            }

            yield return parts[^1];
        }

        private void CheckForeignKeyOnColumn(
            ResolvedTable sourceTable,
            string sourceColumnName,
            ResolvedTable joinedTable,
            string joinedColumnName,
            BooleanComparisonExpression diagnosticTarget)
        {
            var fks = schema.GetForeignKeys(sourceTable);
            foreach (var fk in fks)
            {
                var idx = FindColumnIndex(fk.SourceColumns, sourceColumnName);
                if (idx < 0 || idx >= fk.TargetColumns.Count)
                {
                    continue;
                }

                if (!string.Equals(fk.TargetColumns[idx], joinedColumnName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!TablesAreEqual(joinedTable, fk.TargetTable))
                {
                    AddDiagnostic(
                        fragment: diagnosticTarget,
                        message: $"Column '{sourceTable.SchemaName}.{sourceTable.TableName}.{sourceColumnName}' has a foreign key ('{fk.Name}') to '{fk.TargetTable.SchemaName}.{fk.TargetTable.TableName}.{fk.TargetColumns[idx]}', but this JOIN targets '{joinedTable.SchemaName}.{joinedTable.TableName}' instead.",
                        code: "join-foreign-key-mismatch",
                        category: "Schema",
                        fixable: false
                    );
                }
            }

            var refFks = schema.GetReferencingForeignKeys(sourceTable);
            foreach (var fk in refFks)
            {
                var idx = FindColumnIndex(fk.TargetColumns, sourceColumnName);
                if (idx < 0 || idx >= fk.SourceColumns.Count)
                {
                    continue;
                }

                if (!string.Equals(fk.SourceColumns[idx], joinedColumnName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!TablesAreEqual(joinedTable, fk.SourceTable))
                {
                    AddDiagnostic(
                        fragment: diagnosticTarget,
                        message: $"Column '{sourceTable.SchemaName}.{sourceTable.TableName}.{sourceColumnName}' is referenced by foreign key ('{fk.Name}') from '{fk.SourceTable.SchemaName}.{fk.SourceTable.TableName}.{fk.SourceColumns[idx]}', but this JOIN targets '{joinedTable.SchemaName}.{joinedTable.TableName}' instead.",
                        code: "join-foreign-key-mismatch",
                        category: "Schema",
                        fixable: false
                    );
                }
            }
        }

        private static int FindColumnIndex(IReadOnlyList<string> columns, string columnName)
        {
            for (var i = 0; i < columns.Count; i++)
            {
                if (string.Equals(columns[i], columnName, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool TablesAreEqual(ResolvedTable a, ResolvedTable b)
        {
            return string.Equals(a.DatabaseName, b.DatabaseName, StringComparison.OrdinalIgnoreCase)
                && string.Equals(a.SchemaName, b.SchemaName, StringComparison.OrdinalIgnoreCase)
                && string.Equals(a.TableName, b.TableName, StringComparison.OrdinalIgnoreCase);
        }
    }
}
