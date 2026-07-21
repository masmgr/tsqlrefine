using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers.Schema;
using TsqlRefine.Schema.Model;
using TsqlRefine.Schema.Resolution;

namespace TsqlRefine.Rules.Rules.Schema;

/// <summary>Detects INSERT statements that omit columns requiring explicit values.</summary>
public sealed class InsertMissingRequiredColumnRule : SchemaAwareVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        "insert-missing-required-column",
        "Detects INSERT statements that omit non-nullable columns without generated or default values.",
        "Schema",
        RuleSeverity.Error,
        false);

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new InsertMissingRequiredColumnVisitor(context.Schema!);

    private sealed class InsertMissingRequiredColumnVisitor(ISchemaProvider schema) : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(InsertStatement node)
        {
            var specification = node.InsertSpecification;
            var target = specification?.Target as NamedTableReference;
            var table = DmlWriteTargetHelpers.ResolveNamedTarget(schema, specification?.Target);
            if (specification is null || target is null || table is null || table.IsView ||
                schema is not IColumnSchemaProvider snapshotProvider ||
                !HasExplicitOmissionCheck(specification))
            {
                base.ExplicitVisit(node);
                return;
            }

            var supplied = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var column in specification.Columns)
            {
                var name = DmlWriteTargetHelpers.GetColumnName(column);
                var resolved = name is null ? null : schema.ResolveColumn(table, name);
                if (resolved is null)
                {
                    // Avoid cascading diagnostics with insert-column-not-in-table.
                    base.ExplicitVisit(node);
                    return;
                }
                supplied.Add(resolved.Column.Name);
            }

            var missing = snapshotProvider.GetColumnSchemas(table)
                .Where(IsRequiredForInsert)
                .Where(column => !supplied.Contains(column.Name))
                .Select(column => column.Name)
                .ToArray();
            if (missing.Length > 0)
            {
                AddDiagnostic(
                    target.SchemaObject,
                    $"INSERT into '{table.SchemaName}.{table.TableName}' omits required column(s): {string.Join(", ", missing)}.");
            }

            base.ExplicitVisit(node);
        }

        private static bool HasExplicitOmissionCheck(InsertSpecification specification) =>
            specification.Columns.Count > 0 ||
            specification.InsertSource is ValuesInsertSource { IsDefaultValues: true };

        private static bool IsRequiredForInsert(ColumnSchema column) =>
            !column.IsNullable &&
            !column.IsIdentity &&
            !column.IsComputed &&
            column.DefaultExpression is null &&
            !IsAutomaticallyGeneratedType(column.Type.TypeName);

        private static bool IsAutomaticallyGeneratedType(string typeName) =>
            typeName.Equals("rowversion", StringComparison.OrdinalIgnoreCase) ||
            typeName.Equals("timestamp", StringComparison.OrdinalIgnoreCase);
    }
}
