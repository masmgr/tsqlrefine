using System.Collections.Frozen;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Style;

public sealed class JoinKeywordRule : IRule
{
    private const string RuleId = "join-keyword";
    private const string Category = "Style";

    /// <summary>
    /// Keywords that terminate the FROM clause table list.
    /// </summary>
    private static readonly FrozenSet<string> FromClauseTerminators = FrozenSet.ToFrozenSet(
        ["WHERE", "ORDER", "GROUP", "HAVING", "UNION", "EXCEPT", "INTERSECT",
         "SELECT", "UPDATE", "DELETE", "INSERT", "SET"],
        StringComparer.OrdinalIgnoreCase);

    public RuleMetadata Metadata { get; } = new(
        RuleId: RuleId,
        Description: "Detects comma-separated table lists in FROM clause (implicit joins) and suggests using explicit JOIN syntax for better readability",
        Category: Category,
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: false
    );

    public IEnumerable<Diagnostic> Analyze(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var tokens = context.Tokens;
        if (tokens is null || tokens.Count == 0)
        {
            yield break;
        }

        var analyzer = new CommaJoinAnalyzer(tokens);
        foreach (var commaToken in analyzer.FindCommaJoinTokens())
        {
            yield return CreateDiagnostic(commaToken);
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private Diagnostic CreateDiagnostic(Token commaToken)
    {
        var start = commaToken.Start;
        var end = TokenHelpers.GetTokenEnd(commaToken);

        return new Diagnostic(
            Range: new PluginSdk.Range(start, end),
            Message: "Avoid implicit joins using comma-separated table lists. Use explicit INNER JOIN, LEFT JOIN, or CROSS JOIN syntax for better readability and to prevent accidental Cartesian products.",
            Severity: null,
            Code: RuleId,
            Data: new DiagnosticData(RuleId, Category, Metadata.Fixable)
        );
    }

    /// <summary>
    /// Encapsulates the state machine logic for finding comma joins in FROM clauses.
    /// Tracks parenthesis depth to handle subqueries correctly.
    /// </summary>
    private sealed class CommaJoinAnalyzer(IReadOnlyList<Token> tokens)
    {
        private readonly IReadOnlyList<Token> _tokens = tokens;

        public IEnumerable<Token> FindCommaJoinTokens()
        {
            var parenDepth = 0;

            for (var i = 0; i < _tokens.Count; i++)
            {
                var token = _tokens[i];
                if (TokenHelpers.IsTrivia(token))
                {
                    continue;
                }

                var text = token.Text;

                if (text == "(")
                {
                    parenDepth++;
                    continue;
                }

                if (text == ")")
                {
                    parenDepth = Math.Max(0, parenDepth - 1);
                    continue;
                }

                if (!TokenHelpers.IsKeyword(token, "FROM"))
                {
                    continue;
                }

                foreach (var commaToken in ScanFromClause(i + 1))
                {
                    yield return commaToken;
                }
            }
        }

        /// <summary>
        /// Scans tokens after FROM keyword to find comma joins.
        /// </summary>
        /// <param name="startIndex">Index of the first token after FROM.</param>
        /// <returns>Comma tokens that represent implicit joins.</returns>
        private IEnumerable<Token> ScanFromClause(int startIndex)
        {
            var depth = 0;

            for (var i = startIndex; i < _tokens.Count; i++)
            {
                var token = _tokens[i];
                if (TokenHelpers.IsTrivia(token))
                {
                    continue;
                }

                var text = token.Text;

                if (text == "(")
                {
                    depth++;
                    continue;
                }

                if (text == ")")
                {
                    depth--;
                    if (depth < 0)
                    {
                        yield break;
                    }
                    continue;
                }

                if (depth > 0)
                {
                    continue;
                }

                if (IsFromClauseTerminator(text))
                {
                    yield break;
                }

                if (text == "," && !IsPrecededByJoinKeyword(i))
                {
                    yield return token;
                }
            }
        }

        private static bool IsFromClauseTerminator(string text)
        {
            return text == ";" || FromClauseTerminators.Contains(text);
        }

        /// <summary>
        /// Checks if a comma is preceded by JOIN keyword (within recent tokens).
        /// This distinguishes between comma joins and commas in other contexts.
        /// </summary>
        private bool IsPrecededByJoinKeyword(int commaIndex)
        {
            const int lookbackLimit = 10;
            var searchStart = Math.Max(0, commaIndex - lookbackLimit);

            for (var i = commaIndex - 1; i >= searchStart; i--)
            {
                if (TokenHelpers.IsKeyword(_tokens[i], "JOIN"))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
