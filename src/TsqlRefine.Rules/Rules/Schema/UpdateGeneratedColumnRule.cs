using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers.Schema;

namespace TsqlRefine.Rules.Rules.Schema;

/// <summary>Detects UPDATE writes to computed or identity columns.</summary>
public sealed class UpdateGeneratedColumnRule : SchemaAwareVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        "update-generated-column",
        "Detects UPDATE assignments to computed or identity columns.",
        "Schema",
        RuleSeverity.Error,
        false);

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new UpdateGeneratedColumnVisitor(context.Schema!);

    private sealed class UpdateGeneratedColumnVisitor(ISchemaProvider schema) : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(UpdateStatement node)
        {
            var specification = node.UpdateSpecification;
            var table = specification is null ? null : DmlWriteTargetHelpers.ResolveUpdateTarget(schema, specification);
            if (specification is null || table is null || table.IsView)
            {
                base.ExplicitVisit(node);
                return;
            }

            foreach (var assignment in specification.SetClauses.OfType<AssignmentSetClause>())
            {
                var name = DmlWriteTargetHelpers.GetColumnName(assignment.Column);
                var resolved = name is null ? null : schema.ResolveColumn(table, name);
                if (resolved?.Column.IsComputed == true)
                {
                    AddDiagnostic(assignment.Column, $"Computed column '{resolved.Column.Name}' cannot be assigned by UPDATE.");
                }
                else if (resolved?.Column.IsIdentity == true)
                {
                    AddDiagnostic(assignment.Column, $"Identity column '{resolved.Column.Name}' cannot be assigned by UPDATE.");
                }
            }

            base.ExplicitVisit(node);
        }
    }
}
