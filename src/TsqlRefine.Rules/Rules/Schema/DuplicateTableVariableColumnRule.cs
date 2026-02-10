using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Schema;

/// <summary>
/// Detects duplicate column names in DECLARE @table TABLE variable definitions.
/// Duplicate columns in a table variable always cause a runtime error.
/// </summary>
public sealed class DuplicateTableVariableColumnRule : DiagnosticVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "duplicate-table-variable-column",
        Description: "Detects duplicate column names in DECLARE @table TABLE variable definitions; duplicate columns always cause a runtime error.",
        Category: "Schema",
        DefaultSeverity: RuleSeverity.Error,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new DuplicateTableVariableColumnVisitor();

    public override IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class DuplicateTableVariableColumnVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(DeclareTableVariableStatement node)
        {
            if (node.Body?.Definition?.ColumnDefinitions != null)
            {
                foreach (var duplicate in DuplicateNameAnalysisHelpers.FindDuplicateNames(
                    node.Body.Definition.ColumnDefinitions,
                    column => column.ColumnIdentifier?.Value))
                {
                    AddDiagnostic(
                        fragment: duplicate.Item,
                        message: $"Column '{duplicate.Name}' is defined more than once in the same DECLARE TABLE variable.",
                        code: "duplicate-table-variable-column",
                        category: "Schema",
                        fixable: false
                    );
                }
            }

            base.ExplicitVisit(node);
        }
    }
}
