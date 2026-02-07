using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Style;

public sealed class PreferConcatOverPlusRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "prefer-concat-over-plus",
        Description: "Recommends CONCAT() when + concatenation uses ISNULL/COALESCE; avoids subtle NULL propagation (SQL Server 2012+).",
        Category: "Style",
        DefaultSeverity: RuleSeverity.Information,
        Fixable: false
    );

    public IEnumerable<Diagnostic> Analyze(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // CONCAT is available in SQL Server 2012+ (CompatLevel 110+)
        if (context.CompatLevel < 110)
        {
            yield break;
        }

        if (context.Ast.Fragment is null)
        {
            yield break;
        }

        var visitor = new PreferConcatOverPlusVisitor();
        context.Ast.Fragment.Accept(visitor);

        foreach (var diagnostic in visitor.Diagnostics)
        {
            yield return diagnostic;
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class PreferConcatOverPlusVisitor : DiagnosticVisitorBase
    {
        private readonly HashSet<BinaryExpression> _reported = new();

        public override void ExplicitVisit(BinaryExpression node)
        {
            // Check for string concatenation with +
            if (node.BinaryExpressionType == BinaryExpressionType.Add && !_reported.Contains(node))
            {
                // Check if expression contains ISNULL or COALESCE
                if (ContainsNullHandling(node))
                {
                    // Mark all descendant Add expressions as reported
                    MarkDescendantsAsReported(node);

                    AddDiagnostic(
                        fragment: node,
                        message: "Use CONCAT() instead of + concatenation with ISNULL/COALESCE; it handles NULL values automatically.",
                        code: "prefer-concat-over-plus",
                        category: "Style",
                        fixable: false
                    );
                }
            }

            base.ExplicitVisit(node);
        }

        private void MarkDescendantsAsReported(BinaryExpression node)
        {
            _reported.Add(node);

            if (node.FirstExpression is BinaryExpression leftBinary &&
                leftBinary.BinaryExpressionType == BinaryExpressionType.Add)
            {
                MarkDescendantsAsReported(leftBinary);
            }

            if (node.SecondExpression is BinaryExpression rightBinary &&
                rightBinary.BinaryExpressionType == BinaryExpressionType.Add)
            {
                MarkDescendantsAsReported(rightBinary);
            }
        }

        private static bool ContainsNullHandling(ScalarExpression expression)
        {
            // Check for ISNULL function call
            if (expression is FunctionCall func)
            {
                var funcName = func.FunctionName.Value;
                if (funcName.Equals("ISNULL", StringComparison.OrdinalIgnoreCase) ||
                    funcName.Equals("COALESCE", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            // Check for COALESCE expression (ScriptDom has a separate type for it)
            if (expression is CoalesceExpression)
            {
                return true;
            }

            // Recursively check binary expressions
            if (expression is BinaryExpression binary)
            {
                return ContainsNullHandling(binary.FirstExpression) || ContainsNullHandling(binary.SecondExpression);
            }

            return false;
        }
    }
}
