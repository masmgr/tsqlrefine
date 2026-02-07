using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Correctness;

public sealed class UnreachableCaseWhenRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "unreachable-case-when",
        Description: "Detects duplicate WHEN conditions in CASE expressions that make later branches unreachable.",
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

        var visitor = new UnreachableCaseWhenVisitor(context.Ast.RawSql);
        context.Ast.Fragment.Accept(visitor);

        foreach (var diagnostic in visitor.Diagnostics)
        {
            yield return diagnostic;
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class UnreachableCaseWhenVisitor(string originalSql) : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(SimpleCaseExpression node)
        {
            CheckForDuplicateWhenClauses(node.WhenClauses, w => GetFragmentText(w.WhenExpression));
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(SearchedCaseExpression node)
        {
            CheckForDuplicateWhenClauses(node.WhenClauses, w => GetFragmentText(w.WhenExpression));
            base.ExplicitVisit(node);
        }

        private void CheckForDuplicateWhenClauses<T>(IList<T> whenClauses, Func<T, string?> getConditionText)
            where T : TSqlFragment
        {
            var seenConditions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var clause in whenClauses)
            {
                var conditionText = getConditionText(clause);
                if (conditionText is null)
                {
                    continue;
                }

                // Normalize whitespace for comparison
                var normalized = NormalizeWhitespace(conditionText);

                if (!seenConditions.Add(normalized))
                {
                    AddDiagnostic(
                        fragment: clause,
                        message: $"Duplicate WHEN condition '{conditionText.Trim()}' makes this branch unreachable. The first matching WHEN branch will always be used.",
                        code: "unreachable-case-when",
                        category: "Correctness",
                        fixable: false
                    );
                }
            }
        }

        private string? GetFragmentText(TSqlFragment? fragment)
        {
            if (fragment is null)
            {
                return null;
            }

            var startOffset = fragment.StartOffset;
            var length = fragment.FragmentLength;

            if (startOffset >= 0 && length > 0 && startOffset + length <= originalSql.Length)
            {
                return originalSql.Substring(startOffset, length);
            }

            return null;
        }

        private static string NormalizeWhitespace(string text)
        {
            var sb = new System.Text.StringBuilder(text.Length);
            var lastWasWhitespace = false;

            foreach (var c in text)
            {
                if (char.IsWhiteSpace(c))
                {
                    if (!lastWasWhitespace)
                    {
                        sb.Append(' ');
                        lastWasWhitespace = true;
                    }
                }
                else
                {
                    sb.Append(c);
                    lastWasWhitespace = false;
                }
            }

            return sb.ToString().Trim();
        }
    }
}
