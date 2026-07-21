using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Schema;

/// <summary>Requires DEFAULT constraints on permanent table columns to have explicit names.</summary>
public sealed class RequireNamedDefaultConstraintRule : DiagnosticVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "require-named-default-constraint",
        Description: "Requires DEFAULT constraints on permanent table columns to have explicit names.",
        Category: "Schema",
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: false);

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) => new Visitor();

    private sealed class Visitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(CreateTableStatement node)
        {
            CheckDefinition(node.SchemaObjectName?.BaseIdentifier?.Value, node.Definition);

            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(AlterTableAddTableElementStatement node)
        {
            CheckDefinition(node.SchemaObjectName?.BaseIdentifier?.Value, node.Definition);
            base.ExplicitVisit(node);
        }

        private void CheckDefinition(string? tableName, TableDefinition? definition)
        {
            if (ScriptDomHelpers.IsTemporaryTableName(tableName))
            {
                return;
            }

            foreach (var column in definition?.ColumnDefinitions ?? [])
            {
                if (column.DefaultConstraint is { ConstraintIdentifier: null } defaultConstraint)
                {
                    AddDiagnostic(defaultConstraint, $"DEFAULT constraint for column '{column.ColumnIdentifier?.Value}' must have an explicit name.");
                }
            }
        }
    }
}
