using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers;

namespace TsqlRefine.Rules.Rules.Style;

public sealed class PreferConcatWsRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "prefer-concat-ws",
        Description: "Recommends CONCAT_WS() when concatenation repeats the same separator literal; improves readability and reduces duplication (SQL Server 2017+).",
        Category: "Style",
        DefaultSeverity: RuleSeverity.Information,
        Fixable: false
    );

    public IEnumerable<Diagnostic> Analyze(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // CONCAT_WS is available in SQL Server 2017+ (CompatLevel 140+)
        if (context.CompatLevel < 140)
        {
            yield break;
        }

        if (context.Ast.Fragment is null)
        {
            yield break;
        }

        var visitor = new PreferConcatWsVisitor();
        context.Ast.Fragment.Accept(visitor);

        foreach (var diagnostic in visitor.Diagnostics)
        {
            yield return diagnostic;
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class PreferConcatWsVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(BinaryExpression node)
        {
            // Check for string concatenation with +
            if (node.BinaryExpressionType == BinaryExpressionType.Add)
            {
                // Collect all string literals in the concatenation chain
                var literals = new List<string>();
                CollectStringLiterals(node, literals);

                // Check if there are repeated separator patterns
                if (literals.Count >= 3 && HasRepeatedSeparator(literals))
                {
                    AddDiagnostic(
                        fragment: node,
                        message: "Use CONCAT_WS() when concatenating with repeated separators; it's clearer and reduces duplication.",
                        code: "prefer-concat-ws",
                        category: "Style",
                        fixable: false
                    );
                }
            }

            base.ExplicitVisit(node);
        }

        private static void CollectStringLiterals(ScalarExpression expression, List<string> literals)
        {
            if (expression is StringLiteral strLit)
            {
                literals.Add(strLit.Value);
            }
            else if (expression is BinaryExpression binary && binary.BinaryExpressionType == BinaryExpressionType.Add)
            {
                CollectStringLiterals(binary.FirstExpression, literals);
                CollectStringLiterals(binary.SecondExpression, literals);
            }
        }

        private static bool HasRepeatedSeparator(List<string> literals)
        {
            // Look for patterns like: value + ',' + value + ',' + value
            // Find the most common single-character or short string
            var candidates = literals.Where(l => l.Length <= 3).ToList();
            if (candidates.Count < 2)
            {
                return false;
            }

            var frequencyMap = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var candidate in candidates)
            {
                if (frequencyMap.ContainsKey(candidate))
                {
                    frequencyMap[candidate]++;
                }
                else
                {
                    frequencyMap[candidate] = 1;
                }
            }

            // If any separator appears 2+ times, suggest CONCAT_WS
            return frequencyMap.Values.Any(count => count >= 2);
        }
    }
}
