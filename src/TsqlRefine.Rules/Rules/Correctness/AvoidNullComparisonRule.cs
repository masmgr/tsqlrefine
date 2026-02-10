using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Correctness;

/// <summary>
/// Detects NULL comparisons using = or &lt;&gt; instead of IS NULL/IS NOT NULL, which always evaluate to UNKNOWN.
/// </summary>
public sealed class AvoidNullComparisonRule : DiagnosticVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "avoid-null-comparison",
        Description: "Detects NULL comparisons using = or <> instead of IS NULL/IS NOT NULL, which always evaluate to UNKNOWN.",
        Category: "Correctness",
        DefaultSeverity: RuleSeverity.Error,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new AvoidNullComparisonVisitor();

    public override IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class AvoidNullComparisonVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(BooleanComparisonExpression node)
        {
            // Check if the comparison type is one of: Equals, NotEqualToBrackets, NotEqualToExclamation
            var isInvalidComparison = node.ComparisonType == BooleanComparisonType.Equals ||
                                     node.ComparisonType == BooleanComparisonType.NotEqualToBrackets ||
                                     node.ComparisonType == BooleanComparisonType.NotEqualToExclamation;

            if (isInvalidComparison)
            {
                // Check if either side is a NULL literal
                var hasNullLiteral = node.FirstExpression is NullLiteral ||
                                    node.SecondExpression is NullLiteral;

                if (hasNullLiteral)
                {
                    var comparisonOperator = node.ComparisonType switch
                    {
                        BooleanComparisonType.Equals => "=",
                        BooleanComparisonType.NotEqualToBrackets => "<>",
                        BooleanComparisonType.NotEqualToExclamation => "!=",
                        _ => "comparison"
                    };

                    var suggestedOperator = node.ComparisonType == BooleanComparisonType.Equals
                        ? "IS NULL"
                        : "IS NOT NULL";

                    AddDiagnostic(
                        fragment: node,
                        message: $"NULL comparison using '{comparisonOperator}' always evaluates to UNKNOWN. Use '{suggestedOperator}' instead.",
                        code: "avoid-null-comparison",
                        category: "Correctness",
                        fixable: false
                    );
                }
            }

            base.ExplicitVisit(node);
        }
    }
}
