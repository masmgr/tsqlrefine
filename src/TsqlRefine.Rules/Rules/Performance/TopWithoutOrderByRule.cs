using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Performance;

/// <summary>
/// Detects TOP clause without ORDER BY, which produces non-deterministic results.
/// </summary>
public sealed class TopWithoutOrderByRule : DiagnosticVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "top-without-order-by",
        Description: "Detects TOP clause without ORDER BY, which produces non-deterministic results.",
        Category: "Performance",
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new TopWithoutOrderByVisitor();

    public override IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class TopWithoutOrderByVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(QuerySpecification node)
        {
            if (node.TopRowFilter != null &&
                node.OrderByClause == null &&
                !IsTopZero(node.TopRowFilter.Expression))
            {
                AddDiagnostic(
                    fragment: node.TopRowFilter,
                    message: "TOP clause without ORDER BY produces non-deterministic results. Add an ORDER BY clause to ensure consistent results.",
                    code: "top-without-order-by",
                    category: "Performance",
                    fixable: false
                );
            }

            base.ExplicitVisit(node);
        }

        private static bool IsTopZero(ScalarExpression expression) =>
            expression is IntegerLiteral lit && lit.Value == "0" ||
            expression is ParenthesisExpression paren && IsTopZero(paren.Expression);
    }
}
