using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Correctness;

/// <summary>
/// INSERT SELECT statements must explicitly specify the column list to avoid errors when table schema changes
/// </summary>
public sealed class RequireColumnListForInsertSelectRule : DiagnosticVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "require-column-list-for-insert-select",
        Description: "INSERT SELECT statements must explicitly specify the column list to avoid errors when table schema changes",
        Category: "Correctness",
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new RequireColumnListForInsertSelectVisitor();

    public override IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class RequireColumnListForInsertSelectVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(InsertStatement node)
        {
            if (node.InsertSpecification?.InsertSource is SelectInsertSource)
            {
                var columns = node.InsertSpecification.Columns;
                if (columns is null || columns.Count == 0)
                {
                    AddDiagnostic(
                        fragment: node,
                        message: "INSERT SELECT statement without explicit column list. Specify column names to prevent errors when table schema changes.",
                        code: "require-column-list-for-insert-select",
                        category: "Correctness",
                        fixable: false
                    );
                }
            }

            base.ExplicitVisit(node);
        }
    }
}
