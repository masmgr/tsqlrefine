using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers;

namespace TsqlRefine.Rules.Rules.Style;

public sealed class JoinKeywordRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "join-keyword",
        Description: "Detects comma-separated table lists in FROM clause (implicit joins) and suggests using explicit JOIN syntax for better readability",
        Category: "Style",
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: false
    );

    public IEnumerable<Diagnostic> Analyze(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Use token-based detection for comma joins
        var ranges = FindCommaJoins(context.Tokens);
        foreach (var range in ranges)
        {
            yield return new Diagnostic(
                Range: range,
                Message: "Avoid implicit joins using comma-separated table lists. Use explicit INNER JOIN, LEFT JOIN, or CROSS JOIN syntax for better readability and to prevent accidental Cartesian products.",
                Severity: null,
                Code: Metadata.RuleId,
                Data: new DiagnosticData(Metadata.RuleId, Metadata.Category, Metadata.Fixable)
            );
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private static List<TsqlRefine.PluginSdk.Range> FindCommaJoins(IReadOnlyList<Token> tokens)
    {
        var ranges = new List<TsqlRefine.PluginSdk.Range>();

        if (tokens is null || tokens.Count == 0)
        {
            return ranges;
        }

        // Track parenthesis depth across the entire token stream
        var currentDepth = 0;

        for (var i = 0; i < tokens.Count; i++)
        {
            if (TokenHelpers.IsTrivia(tokens[i]))
            {
                continue;
            }

            var text = tokens[i].Text;

            // Track parenthesis depth
            if (text == "(")
            {
                currentDepth++;
                continue;
            }

            if (text == ")")
            {
                currentDepth--;
                continue;
            }

            // Look for FROM keyword
            if (!TokenHelpers.IsKeyword(tokens[i], "FROM"))
            {
                continue;
            }

            // Remember the depth at which we found FROM (for subquery detection)
            var fromDepth = currentDepth;

            // Look for commas between FROM and WHERE/ORDER BY/GROUP BY/HAVING/;
            var depth = 0;
            for (var j = i + 1; j < tokens.Count; j++)
            {
                if (TokenHelpers.IsTrivia(tokens[j]))
                {
                    continue;
                }

                text = tokens[j].Text;

                // Track parenthesis depth (for subqueries)
                if (text == "(")
                {
                    depth++;
                    continue;
                }

                if (text == ")")
                {
                    depth--;
                    // If we've exited the subquery that contained this FROM, stop scanning
                    if (depth < 0)
                    {
                        break;
                    }
                    continue;
                }

                // At depth 0, check for clause terminators or new statement start
                if (depth == 0)
                {
                    if (TokenHelpers.IsKeyword(tokens[j], "WHERE") ||
                        TokenHelpers.IsKeyword(tokens[j], "ORDER") ||
                        TokenHelpers.IsKeyword(tokens[j], "GROUP") ||
                        TokenHelpers.IsKeyword(tokens[j], "HAVING") ||
                        TokenHelpers.IsKeyword(tokens[j], "UNION") ||
                        TokenHelpers.IsKeyword(tokens[j], "EXCEPT") ||
                        TokenHelpers.IsKeyword(tokens[j], "INTERSECT") ||
                        TokenHelpers.IsKeyword(tokens[j], "SELECT") ||
                        TokenHelpers.IsKeyword(tokens[j], "UPDATE") ||
                        TokenHelpers.IsKeyword(tokens[j], "DELETE") ||
                        TokenHelpers.IsKeyword(tokens[j], "INSERT") ||
                        TokenHelpers.IsKeyword(tokens[j], "SET") ||
                        text == ";")
                    {
                        break;
                    }

                    // Found comma in FROM clause (not in subquery)
                    if (text == "," && !IsPartOfJoin(tokens, j))
                    {
                        var start = tokens[j].Start;
                        var end = TokenHelpers.GetTokenEnd(tokens[j]);
                        ranges.Add(new TsqlRefine.PluginSdk.Range(start, end));
                    }
                }
            }
        }

        return ranges;
    }

    private static bool IsPartOfJoin(IReadOnlyList<Token> tokens, int commaIndex)
    {
        // Check if the comma is preceded by JOIN keyword (within a few tokens)
        for (var i = Math.Max(0, commaIndex - 10); i < commaIndex; i++)
        {
            if (TokenHelpers.IsKeyword(tokens[i], "JOIN"))
            {
                return true;
            }
        }

        return false;
    }
}
