using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Correctness;

/// <summary>
/// Warns when LEN() is compared against zero for emptiness checks.
/// LEN() ignores trailing spaces, so whitespace-only values (including full-width spaces)
/// can slip through an emptiness check; DATALENGTH() should be used to detect them reliably.
/// </summary>
public sealed class LenForEmptinessCheckRule : DiagnosticVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "len-for-emptiness-check",
        Description: "Warns when LEN() is used in an emptiness comparison; trailing spaces are ignored, so use DATALENGTH() to detect whitespace-only values.",
        Category: "Correctness",
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new LenForEmptinessCheckVisitor(Metadata);

    public override IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class LenForEmptinessCheckVisitor(RuleMetadata metadata) : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(BooleanComparisonExpression node)
        {
            // Detect LEN(...) on one side compared against zero on the other.
            // Restricting the opposite operand to zero keeps the intent clear and avoids
            // changing regular character-count checks such as LEN(code) < 5.
            var lenCall = GetLenCallComparedToZero(node.FirstExpression, node.SecondExpression)
                ?? GetLenCallComparedToZero(node.SecondExpression, node.FirstExpression);

            if (lenCall is not null)
            {
                AddDiagnostic(
                    fragment: lenCall,
                    message: "LEN() ignores trailing spaces; use DATALENGTH() to reliably detect empty or whitespace-only values.",
                    code: metadata.RuleId,
                    category: metadata.Category,
                    fixable: metadata.Fixable
                );
            }

            base.ExplicitVisit(node);
        }

        private static FunctionCall? GetLenCallComparedToZero(
            ScalarExpression candidate,
            ScalarExpression other)
        {
            if (other is not IntegerLiteral integerLiteral ||
                !int.TryParse(integerLiteral.Value, out var value) ||
                value != 0)
            {
                return null;
            }

            if (candidate is FunctionCall funcCall &&
                funcCall.FunctionName.Value.Equals("LEN", StringComparison.OrdinalIgnoreCase))
            {
                return funcCall;
            }

            return null;
        }
    }
}
