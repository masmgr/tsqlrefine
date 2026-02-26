using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Schema;

/// <summary>
/// Detects UPDATE statements that reference columns not found in the target table.
/// </summary>
public sealed class UpdateColumnNotInTableRule : SchemaAwareVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "update-column-not-in-table",
        Description: "Detects UPDATE statements that reference columns not found in the target table.",
        Category: "Schema",
        DefaultSeverity: RuleSeverity.Error,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new UpdateColumnNotInTableVisitor(context.Schema!);

    private sealed class UpdateColumnNotInTableVisitor(ISchemaProvider schema) : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(UpdateStatement node)
        {
            var updateSpec = node.UpdateSpecification;
            var target = updateSpec?.Target as NamedTableReference;
            if (target is null || updateSpec is null)
            {
                base.ExplicitVisit(node);
                return;
            }

            var schemaObject = target.SchemaObject;
            var tableName = schemaObject.BaseIdentifier?.Value;
            if (tableName is null || tableName.StartsWith('#') || tableName.StartsWith('@'))
            {
                base.ExplicitVisit(node);
                return;
            }

            var schemaName = schemaObject.SchemaIdentifier?.Value;
            var dbName = schemaObject.DatabaseIdentifier?.Value;

            var resolvedTable = schema.ResolveTable(dbName, schemaName, tableName);
            if (resolvedTable is null)
            {
                // Table not found — skip (reported by unresolved-table-reference)
                base.ExplicitVisit(node);
                return;
            }

            var setClauses = updateSpec.SetClauses;
            if (setClauses is null or { Count: 0 })
            {
                base.ExplicitVisit(node);
                return;
            }

            foreach (var setClause in setClauses)
            {
                if (setClause is AssignmentSetClause assignment)
                {
                    var colRef = assignment.Column;
                    var columnName = colRef?.MultiPartIdentifier?.Identifiers.LastOrDefault()?.Value;
                    if (columnName is null)
                    {
                        continue;
                    }

                    var resolvedColumn = schema.ResolveColumn(resolvedTable, columnName);
                    if (resolvedColumn is null)
                    {
                        AddDiagnostic(
                            fragment: colRef!,
                            message: $"Column '{columnName}' not found in '{resolvedTable.SchemaName}.{resolvedTable.TableName}'.",
                            code: "update-column-not-in-table",
                            category: "Schema",
                            fixable: false
                        );
                    }
                }
            }

            base.ExplicitVisit(node);
        }
    }
}
