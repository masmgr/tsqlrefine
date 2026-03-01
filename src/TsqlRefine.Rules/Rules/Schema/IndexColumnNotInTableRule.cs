using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers.Schema;

namespace TsqlRefine.Rules.Rules.Schema;

/// <summary>
/// Detects index definitions that reference columns not found in the target table.
/// Handles both standalone CREATE INDEX statements (schema-aware) and
/// inline index definitions in CREATE TABLE (validated against the table definition itself).
/// </summary>
public sealed class IndexColumnNotInTableRule : DiagnosticVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "index-column-not-in-table",
        Description: "Detects index definitions that reference columns not found in the target table.",
        Category: "Schema",
        DefaultSeverity: RuleSeverity.Error,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new IndexColumnNotInTableVisitor(context.Schema);

    private sealed class IndexColumnNotInTableVisitor(ISchemaProvider? schema) : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(CreateIndexStatement node)
        {
            if (schema is null)
            {
                base.ExplicitVisit(node);
                return;
            }

            var onName = node.OnName;
            if (onName is null)
            {
                base.ExplicitVisit(node);
                return;
            }

            var tableName = onName.BaseIdentifier?.Value;
            if (tableName is null || AliasMapBuilder.IsTemporaryOrVariable(tableName))
            {
                base.ExplicitVisit(node);
                return;
            }

            var schemaName = onName.SchemaIdentifier?.Value;
            var dbName = onName.DatabaseIdentifier?.Value;

            var resolvedTable = schema.ResolveTable(dbName, schemaName, tableName);
            if (resolvedTable is null)
            {
                base.ExplicitVisit(node);
                return;
            }

            var displayName = $"{resolvedTable.SchemaName}.{resolvedTable.TableName}";

            ValidateKeyColumns(node.Columns, resolvedTable, displayName);
            ValidateIncludeColumns(node.IncludeColumns, resolvedTable, displayName);

            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(CreateTableStatement node)
        {
            if (node.Definition?.Indexes is not { Count: > 0 } indexes)
            {
                base.ExplicitVisit(node);
                return;
            }

            var tableNameValue = node.SchemaObjectName?.BaseIdentifier?.Value;
            if (tableNameValue is null || AliasMapBuilder.IsTemporaryOrVariable(tableNameValue))
            {
                base.ExplicitVisit(node);
                return;
            }

            var definedColumns = CollectDefinedColumns(node.Definition.ColumnDefinitions);
            if (definedColumns.Count == 0)
            {
                base.ExplicitVisit(node);
                return;
            }

            var schemaNameValue = node.SchemaObjectName?.SchemaIdentifier?.Value;
            var displayName = schemaNameValue is not null
                ? $"{schemaNameValue}.{tableNameValue}"
                : tableNameValue;

            foreach (var index in indexes)
            {
                ValidateKeyColumnsAgainstDefinition(index.Columns, definedColumns, displayName);
                ValidateIncludeColumnsAgainstDefinition(index.IncludeColumns, definedColumns, displayName);
            }

            base.ExplicitVisit(node);
        }

        private void ValidateKeyColumns(
            IList<ColumnWithSortOrder>? columns,
            ResolvedTable resolvedTable,
            string displayName)
        {
            if (columns is null)
            {
                return;
            }

            foreach (var col in columns)
            {
                var columnName = col.Column?.MultiPartIdentifier?.Identifiers.LastOrDefault()?.Value;
                if (columnName is null)
                {
                    continue;
                }

                if (schema!.ResolveColumn(resolvedTable, columnName) is null)
                {
                    AddDiagnostic(
                        fragment: col,
                        message: $"Column '{columnName}' not found in '{displayName}'.",
                        code: "index-column-not-in-table",
                        category: "Schema",
                        fixable: false);
                }
            }
        }

        private void ValidateIncludeColumns(
            IList<ColumnReferenceExpression>? columns,
            ResolvedTable resolvedTable,
            string displayName)
        {
            if (columns is null)
            {
                return;
            }

            foreach (var colRef in columns)
            {
                var columnName = colRef.MultiPartIdentifier?.Identifiers.LastOrDefault()?.Value;
                if (columnName is null)
                {
                    continue;
                }

                if (schema!.ResolveColumn(resolvedTable, columnName) is null)
                {
                    AddDiagnostic(
                        fragment: colRef,
                        message: $"Column '{columnName}' not found in '{displayName}'.",
                        code: "index-column-not-in-table",
                        category: "Schema",
                        fixable: false);
                }
            }
        }

        private void ValidateKeyColumnsAgainstDefinition(
            IList<ColumnWithSortOrder>? columns,
            HashSet<string> definedColumns,
            string displayName)
        {
            if (columns is null)
            {
                return;
            }

            foreach (var col in columns)
            {
                var columnName = col.Column?.MultiPartIdentifier?.Identifiers.LastOrDefault()?.Value;
                if (columnName is null)
                {
                    continue;
                }

                if (!definedColumns.Contains(columnName))
                {
                    AddDiagnostic(
                        fragment: col,
                        message: $"Column '{columnName}' not found in table definition for '{displayName}'.",
                        code: "index-column-not-in-table",
                        category: "Schema",
                        fixable: false);
                }
            }
        }

        private void ValidateIncludeColumnsAgainstDefinition(
            IList<ColumnReferenceExpression>? columns,
            HashSet<string> definedColumns,
            string displayName)
        {
            if (columns is null)
            {
                return;
            }

            foreach (var colRef in columns)
            {
                var columnName = colRef.MultiPartIdentifier?.Identifiers.LastOrDefault()?.Value;
                if (columnName is null)
                {
                    continue;
                }

                if (!definedColumns.Contains(columnName))
                {
                    AddDiagnostic(
                        fragment: colRef,
                        message: $"Column '{columnName}' not found in table definition for '{displayName}'.",
                        code: "index-column-not-in-table",
                        category: "Schema",
                        fixable: false);
                }
            }
        }

        private static HashSet<string> CollectDefinedColumns(IList<ColumnDefinition>? columnDefinitions)
        {
            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (columnDefinitions is null)
            {
                return columns;
            }

            foreach (var colDef in columnDefinitions)
            {
                var name = colDef.ColumnIdentifier?.Value;
                if (name is not null)
                {
                    columns.Add(name);
                }
            }

            return columns;
        }
    }
}
