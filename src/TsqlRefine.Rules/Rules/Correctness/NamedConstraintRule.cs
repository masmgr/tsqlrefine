using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Correctness;

/// <summary>
/// Prohibit named constraints in temp tables to avoid naming conflicts
/// </summary>
public sealed class NamedConstraintRule : DiagnosticVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "avoid-named-constraint-in-temp-table",
        Description: "Prohibit named constraints in temp tables to avoid naming conflicts",
        Category: "Correctness",
        DefaultSeverity: RuleSeverity.Error,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new NamedConstraintVisitor();

    public override IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class NamedConstraintVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(CreateTableStatement node)
        {
            // Check if table is a temp table (starts with # or ##)
            if (ScriptDomHelpers.IsTemporaryTableName(node.SchemaObjectName?.BaseIdentifier?.Value))
            {
                CheckDefinition(node.Definition);
            }

            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(AlterTableAddTableElementStatement node)
        {
            if (ScriptDomHelpers.IsTemporaryTableName(node.SchemaObjectName?.BaseIdentifier?.Value))
            {
                CheckDefinition(node.Definition);
            }

            base.ExplicitVisit(node);
        }

        private void CheckDefinition(TableDefinition? definition)
        {
            if (definition is null)
            {
                return;
            }

            foreach (var element in definition.TableConstraints.Where(element => element.ConstraintIdentifier is not null))
            {
                AddNamedConstraintDiagnostic(element.ConstraintIdentifier!);
            }

            foreach (var constraint in definition.ColumnDefinitions
                         .SelectMany(column => column.Constraints)
                         .Where(constraint => constraint.ConstraintIdentifier is not null))
            {
                AddNamedConstraintDiagnostic(constraint.ConstraintIdentifier!);
            }
        }

        private void AddNamedConstraintDiagnostic(Identifier identifier) => AddDiagnostic(
            fragment: identifier,
            message: "Named constraint found in temp table. Remove constraint names to avoid naming conflicts.",
            code: "avoid-named-constraint-in-temp-table",
            category: "Correctness",
            fixable: false);
    }
}
