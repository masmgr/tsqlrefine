using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers;

namespace TsqlRefine.Rules.Rules.Style;

public sealed class PreferTryConvertPatternsRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "prefer-try-convert-patterns",
        Description: "Recommends TRY_CONVERT/TRY_CAST over CASE + ISNUMERIC/ISDATE; fewer false positives and clearer intent.",
        Category: "Style",
        DefaultSeverity: RuleSeverity.Information,
        Fixable: false
    );

    public IEnumerable<Diagnostic> Analyze(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Ast.Fragment is null)
        {
            yield break;
        }

        var visitor = new PreferTryConvertPatternsVisitor();
        context.Ast.Fragment.Accept(visitor);

        foreach (var diagnostic in visitor.Diagnostics)
        {
            yield return diagnostic;
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class PreferTryConvertPatternsVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(SearchedCaseExpression node)
        {
            // Check for CASE expressions with ISNUMERIC/ISDATE in WHEN and CONVERT/CAST in THEN
            if (node.WhenClauses != null)
            {
                foreach (var whenClause in node.WhenClauses)
                {
                    if (ContainsValidationFunction(whenClause.WhenExpression) &&
                        ContainsConversion(whenClause.ThenExpression))
                    {
                        AddDiagnostic(
                            fragment: node,
                            message: "Use TRY_CONVERT or TRY_CAST instead of CASE with ISNUMERIC/ISDATE; it's safer and has fewer false positives.",
                            code: "prefer-try-convert-patterns",
                            category: "Style",
                            fixable: false
                        );
                        break;
                    }
                }
            }

            base.ExplicitVisit(node);
        }

        private static bool ContainsValidationFunction(BooleanExpression expression)
        {
            if (expression is BooleanParenthesisExpression parenthesis)
            {
                return ContainsValidationFunction(parenthesis.Expression);
            }

            if (expression is BooleanBinaryExpression binary)
            {
                return ContainsValidationFunction(binary.FirstExpression) || ContainsValidationFunction(binary.SecondExpression);
            }

            if (expression is BooleanComparisonExpression comparison)
            {
                return IsValidationFunctionCall(comparison.FirstExpression) || IsValidationFunctionCall(comparison.SecondExpression);
            }

            return false;
        }

        private static bool IsValidationFunctionCall(ScalarExpression expression)
        {
            if (expression is FunctionCall func)
            {
                var funcName = func.FunctionName.Value;
                return funcName.Equals("ISNUMERIC", StringComparison.OrdinalIgnoreCase) ||
                       funcName.Equals("ISDATE", StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        private static bool ContainsConversion(ScalarExpression expression)
        {
            if (expression is CastCall or ConvertCall)
            {
                return true;
            }

            if (expression is BinaryExpression binary)
            {
                return ContainsConversion(binary.FirstExpression) || ContainsConversion(binary.SecondExpression);
            }

            return false;
        }
    }
}
