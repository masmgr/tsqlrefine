using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Correctness;

/// <summary>
/// Detects mixed AND/OR operators at same precedence level without explicit parentheses to prevent precedence confusion.
/// </summary>
public sealed class RequireParenthesesForMixedAndOrRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "require-parentheses-for-mixed-and-or",
        Description: "Detects mixed AND/OR operators at same precedence level without explicit parentheses to prevent precedence confusion.",
        Category: "Correctness",
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: false
    );

    public IEnumerable<Diagnostic> Analyze(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Ast.Fragment is null)
        {
            yield break;
        }

        var visitor = new MixedAndOrVisitor();
        context.Ast.Fragment.Accept(visitor);

        foreach (var diagnostic in visitor.Diagnostics)
        {
            yield return diagnostic;
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class MixedAndOrVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(BooleanBinaryExpression node)
        {
            // Check if either child is a BooleanBinaryExpression with a different operator
            // that is NOT wrapped in parentheses
            CheckForMixedOperators(node, node.FirstExpression);
            CheckForMixedOperators(node, node.SecondExpression);

            // Continue traversal
            base.ExplicitVisit(node);
        }

        private void CheckForMixedOperators(BooleanBinaryExpression parent, BooleanExpression? child)
        {
            if (child is null)
            {
                return;
            }

            // If the child is wrapped in parentheses, it's fine - parentheses create a new precedence level
            if (child is BooleanParenthesisExpression)
            {
                return;
            }

            // If the child is a BooleanBinaryExpression with a different operator, report
            if (child is BooleanBinaryExpression childBinary)
            {
                if (childBinary.BinaryExpressionType != parent.BinaryExpressionType)
                {
                    AddDiagnostic(
                        fragment: parent,
                        message: "Mixed AND/OR operators without parentheses can cause precedence confusion. Use explicit parentheses to clarify intent.",
                        code: "require-parentheses-for-mixed-and-or",
                        category: "Correctness",
                        fixable: false
                    );
                }
            }
        }
    }
}
