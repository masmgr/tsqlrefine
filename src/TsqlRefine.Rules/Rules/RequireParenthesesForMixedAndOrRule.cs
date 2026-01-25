using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules;

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

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(diagnostic);
        return Array.Empty<Fix>();
    }

    private sealed class MixedAndOrVisitor : TSqlFragmentVisitor
    {
        private readonly List<Diagnostic> _diagnostics = new();

        public IReadOnlyList<Diagnostic> Diagnostics => _diagnostics;

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
                    _diagnostics.Add(new Diagnostic(
                        Range: GetRange(parent),
                        Message: $"Mixed AND/OR operators without parentheses can cause precedence confusion. Use explicit parentheses to clarify intent.",
                        Code: "require-parentheses-for-mixed-and-or",
                        Data: new DiagnosticData("require-parentheses-for-mixed-and-or", "Correctness", false)
                    ));
                }
            }
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
