using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Schema;

/// <summary>
/// Detects duplicate columns within a single FOREIGN KEY constraint definition.
/// </summary>
public sealed class DuplicateForeignKeyColumnRule : DiagnosticVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "duplicate-foreign-key-column",
        Description: "Detects duplicate columns within a single FOREIGN KEY constraint definition.",
        Category: "Schema",
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new DuplicateForeignKeyColumnVisitor();

    public override IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class DuplicateForeignKeyColumnVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(CreateTableStatement node)
        {
            if (node.Definition?.TableConstraints == null)
            {
                base.ExplicitVisit(node);
                return;
            }

            foreach (var constraint in node.Definition.TableConstraints)
            {
                if (constraint is ForeignKeyConstraintDefinition fk)
                {
                    var constraintName = fk.ConstraintIdentifier?.Value;
                    var display = constraintName != null
                        ? $"FOREIGN KEY constraint '{constraintName}'"
                        : "FOREIGN KEY constraint";

                    CheckColumnsForDuplicates(fk.Columns, display);
                }
            }

            base.ExplicitVisit(node);
        }

        private void CheckColumnsForDuplicates(IList<Identifier>? columns, string constraintLabel)
        {
            if (columns == null || columns.Count < 2)
            {
                return;
            }

            foreach (var duplicate in DuplicateNameAnalysisHelpers.FindDuplicateNames(columns, column => column.Value))
            {
                AddDiagnostic(
                    fragment: duplicate.Item,
                    message: $"Column '{duplicate.Name}' is specified more than once in {constraintLabel}.",
                    code: "duplicate-foreign-key-column",
                    category: "Schema",
                    fixable: false
                );
            }
        }
    }
}
