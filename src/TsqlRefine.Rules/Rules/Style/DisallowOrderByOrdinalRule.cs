using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Style;

/// <summary>
/// Forbids ORDER BY with ordinal positions (e.g., ORDER BY 1, 2) which break silently when columns are reordered.
/// </summary>
public sealed class DisallowOrderByOrdinalRule : DiagnosticVisitorRuleBase
{
    private const string RuleId = "disallow-order-by-ordinal";
    private const string Category = "Style";

    public override RuleMetadata Metadata { get; } = new(
        RuleId: RuleId,
        Description: "Forbids ORDER BY with ordinal positions (e.g., ORDER BY 1, 2) which break silently when columns are reordered.",
        Category: Category,
        DefaultSeverity: RuleSeverity.Information,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new DisallowOrderByOrdinalVisitor();

    private sealed class DisallowOrderByOrdinalVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(OrderByClause node)
        {
            foreach (var element in node.OrderByElements)
            {
                if (element is ExpressionWithSortOrder { Expression: IntegerLiteral intLiteral })
                {
                    AddDiagnostic(
                        fragment: intLiteral,
                        message: $"Avoid ORDER BY with ordinal position ({intLiteral.Value}). Use explicit column names instead.",
                        code: RuleId,
                        category: Category,
                        fixable: false
                    );
                }
            }

            base.ExplicitVisit(node);
        }
    }
}
