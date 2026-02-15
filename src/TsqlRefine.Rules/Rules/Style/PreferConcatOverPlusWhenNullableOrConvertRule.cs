using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Style;

/// <summary>
/// Stricter variant that also detects CAST/CONVERT in concatenations; enable instead of prefer-concat-over-plus for comprehensive coverage (SQL Server 2012+).
/// </summary>
public sealed class PreferConcatOverPlusWhenNullableOrConvertRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "prefer-concat-with-nullable",
        Description: "Stricter variant that also detects CAST/CONVERT in concatenations; enable instead of prefer-concat-over-plus for comprehensive coverage (SQL Server 2012+).",
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

        var visitor = new PreferConcatOverPlusWhenNullableOrConvertVisitor();
        context.Ast.Fragment.Accept(visitor);

        foreach (var diagnostic in visitor.Diagnostics)
        {
            yield return diagnostic;
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class PreferConcatOverPlusWhenNullableOrConvertVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(BinaryExpression node)
        {
            // Check for string concatenation with +
            if (node.BinaryExpressionType == BinaryExpressionType.Add)
            {
                // Check if expression contains ISNULL, COALESCE, CAST, or CONVERT
                if (ContainsNullHandlingOrConversion(node.FirstExpression) || ContainsNullHandlingOrConversion(node.SecondExpression))
                {
                    AddDiagnostic(
                        fragment: node,
                        message: "Use CONCAT() instead of + concatenation with ISNULL/COALESCE/CAST/CONVERT; it handles NULL values and type conversions automatically.",
                        code: "prefer-concat-with-nullable",
                        category: "Style",
                        fixable: false
                    );
                }
            }

            base.ExplicitVisit(node);
        }

        private static bool ContainsNullHandlingOrConversion(ScalarExpression expression)
        {
            if (expression is FunctionCall func)
            {
                var funcName = func.FunctionName.Value;
                if (funcName.Equals("ISNULL", StringComparison.OrdinalIgnoreCase) ||
                    funcName.Equals("COALESCE", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            if (expression is CastCall or ConvertCall)
            {
                return true;
            }

            // Recursively check binary expressions
            if (expression is BinaryExpression binary)
            {
                return ContainsNullHandlingOrConversion(binary.FirstExpression) ||
                       ContainsNullHandlingOrConversion(binary.SecondExpression);
            }

            return false;
        }
    }
}
