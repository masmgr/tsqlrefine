using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Schema;

/// <summary>
/// Detects duplicate column names in CREATE VIEW definitions.
/// Duplicate columns in a VIEW always cause a runtime error.
/// </summary>
public sealed class DuplicateViewColumnRule : DiagnosticVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "duplicate-view-column",
        Description: "Detects duplicate column names in CREATE VIEW definitions; duplicate columns always cause a runtime error.",
        Category: "Schema",
        DefaultSeverity: RuleSeverity.Error,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new DuplicateViewColumnVisitor();

    public override IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class DuplicateViewColumnVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(CreateViewStatement node)
        {
            // If the VIEW has an explicit column list, check that for duplicates
            if (node.Columns != null && node.Columns.Count > 0)
            {
                CheckExplicitColumnList(node.Columns);
            }
            else
            {
                // Otherwise check the SELECT elements for duplicate output names
                var querySpec = node.SelectStatement?.QueryExpression as QuerySpecification;
                if (querySpec?.SelectElements != null)
                {
                    CheckSelectElements(querySpec.SelectElements);
                }
            }

            base.ExplicitVisit(node);
        }

        private void CheckExplicitColumnList(IList<Identifier> columns)
        {
            foreach (var duplicate in DuplicateNameAnalysisHelpers.FindDuplicateNames(columns, column => column.Value))
            {
                AddDiagnostic(
                    fragment: duplicate.Item,
                    message: $"Column '{duplicate.Name}' appears more than once in the VIEW column list.",
                    code: "duplicate-view-column",
                    category: "Schema",
                    fixable: false
                );
            }
        }

        private void CheckSelectElements(IList<SelectElement> selectElements)
        {
            foreach (var (element, name) in SelectColumnHelpers.FindDuplicateColumns(selectElements))
            {
                AddDiagnostic(
                    fragment: (TSqlFragment)element,
                    message: $"Column '{name}' appears more than once in the VIEW SELECT list.",
                    code: "duplicate-view-column",
                    category: "Schema",
                    fixable: false
                );
            }
        }
    }
}
