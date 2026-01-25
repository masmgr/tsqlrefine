using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules;

public sealed class AvoidNullComparisonRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "avoid-null-comparison",
        Description: "Detects NULL comparisons using = or <> instead of IS NULL/IS NOT NULL, which always evaluate to UNKNOWN.",
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

        var visitor = new AvoidNullComparisonVisitor();
        context.Ast.Fragment.Accept(visitor);

        foreach (var diagnostic in visitor.Diagnostics)
        {
            yield return diagnostic;
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(diagnostic);
        return Array.Empty<Fix>();
    }

    private sealed class AvoidNullComparisonVisitor : TSqlFragmentVisitor
    {
        private readonly List<Diagnostic> _diagnostics = new();
        public IReadOnlyList<Diagnostic> Diagnostics => _diagnostics;

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

                    _diagnostics.Add(new Diagnostic(
                        Range: GetRange(node),
                        Message: $"NULL comparison using '{comparisonOperator}' always evaluates to UNKNOWN. Use '{suggestedOperator}' instead.",
                        Code: "avoid-null-comparison",
                        Data: new DiagnosticData("avoid-null-comparison", "Correctness", false)
                    ));
                }
            }

            base.ExplicitVisit(node);
        }

        private static TsqlRefine.PluginSdk.Range GetRange(TSqlFragment fragment)
        {
            var start = new Position(fragment.StartLine - 1, fragment.StartColumn - 1);
            var end = start;

            // Try to get the end position from the last token
            if (fragment.ScriptTokenStream != null &&
                fragment.LastTokenIndex >= 0 &&
                fragment.LastTokenIndex < fragment.ScriptTokenStream.Count)
            {
                var lastToken = fragment.ScriptTokenStream[fragment.LastTokenIndex];
                var tokenText = lastToken.Text ?? string.Empty;
                end = new Position(
                    lastToken.Line - 1,
                    lastToken.Column - 1 + tokenText.Length
                );
            }

            return new TsqlRefine.PluginSdk.Range(start, end);
        }
    }
}
