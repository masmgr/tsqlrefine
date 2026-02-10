using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Correctness;

/// <summary>
/// Detects duplicate WHEN conditions in CASE expressions that make later branches unreachable.
/// </summary>
public sealed class UnreachableCaseWhenRule : DiagnosticVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "unreachable-case-when",
        Description: "Detects duplicate WHEN conditions in CASE expressions that make later branches unreachable.",
        Category: "Correctness",
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new UnreachableCaseWhenVisitor();

    public override IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class UnreachableCaseWhenVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(SimpleCaseExpression node)
        {
            CheckForDuplicateWhenClauses(
                node.WhenClauses,
                w => GetConditionSignature(w.WhenExpression),
                w => GetConditionDisplay(w.WhenExpression));
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(SearchedCaseExpression node)
        {
            CheckForDuplicateWhenClauses(
                node.WhenClauses,
                w => GetConditionSignature(w.WhenExpression),
                w => GetConditionDisplay(w.WhenExpression));
            base.ExplicitVisit(node);
        }

        private void CheckForDuplicateWhenClauses<T>(
            IList<T> whenClauses,
            Func<T, string?> getConditionKey,
            Func<T, string?> getConditionDisplay)
            where T : TSqlFragment
        {
            var seenConditions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var clause in whenClauses)
            {
                var conditionKey = getConditionKey(clause);
                if (string.IsNullOrEmpty(conditionKey))
                {
                    continue;
                }

                if (!seenConditions.Add(conditionKey))
                {
                    var displayText = getConditionDisplay(clause) ?? "<expression>";
                    AddDiagnostic(
                        fragment: clause,
                        message: $"Duplicate WHEN condition '{displayText}' makes this branch unreachable. The first matching WHEN branch will always be used.",
                        code: "unreachable-case-when",
                        category: "Correctness",
                        fixable: false
                    );
                }
            }
        }

        private static string? GetConditionSignature(TSqlFragment? fragment)
        {
            if (fragment is null)
            {
                return null;
            }

            var tokenStream = fragment.ScriptTokenStream;
            if (!TryGetTokenSpan(fragment, tokenStream, out var startIndex, out var endIndex))
            {
                return null;
            }

            var sb = new System.Text.StringBuilder();
            for (var i = startIndex; i <= endIndex; i++)
            {
                var token = tokenStream[i];
                if (IsTrivia(token.TokenType))
                {
                    continue;
                }

                if (sb.Length > 0)
                {
                    sb.Append(' ');
                }

                sb.Append(token.Text);
            }

            return sb.ToString();
        }

        private static string? GetConditionDisplay(TSqlFragment? fragment)
        {
            if (fragment is null)
            {
                return null;
            }

            var tokenStream = fragment.ScriptTokenStream;
            if (!TryGetTokenSpan(fragment, tokenStream, out var startIndex, out var endIndex))
            {
                return null;
            }

            var sb = new System.Text.StringBuilder();
            for (var i = startIndex; i <= endIndex; i++)
            {
                var token = tokenStream[i];
                if (token.TokenType == TSqlTokenType.WhiteSpace)
                {
                    if (sb.Length > 0 && sb[^1] != ' ')
                    {
                        sb.Append(' ');
                    }

                    continue;
                }

                if (token.TokenType == TSqlTokenType.SingleLineComment ||
                    token.TokenType == TSqlTokenType.MultilineComment)
                {
                    continue;
                }

                sb.Append(token.Text);
            }

            return sb.ToString().Trim();
        }

        private static bool TryGetTokenSpan(
            TSqlFragment fragment,
            IList<TSqlParserToken>? tokenStream,
            out int startIndex,
            out int endIndex)
        {
            startIndex = -1;
            endIndex = -1;

            if (tokenStream is null || tokenStream.Count == 0)
            {
                return false;
            }

            if (fragment.FirstTokenIndex < 0 || fragment.LastTokenIndex < 0)
            {
                return false;
            }

            if (fragment.FirstTokenIndex >= tokenStream.Count || fragment.LastTokenIndex >= tokenStream.Count)
            {
                return false;
            }

            startIndex = fragment.FirstTokenIndex;
            endIndex = fragment.LastTokenIndex;
            return true;
        }

        private static bool IsTrivia(TSqlTokenType tokenType)
        {
            return tokenType == TSqlTokenType.WhiteSpace ||
                   tokenType == TSqlTokenType.SingleLineComment ||
                   tokenType == TSqlTokenType.MultilineComment;
        }
    }
}
