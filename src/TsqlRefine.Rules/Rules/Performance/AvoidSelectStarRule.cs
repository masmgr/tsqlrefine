using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers;

namespace TsqlRefine.Rules.Rules.Performance;

public sealed class AvoidSelectStarRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "avoid-select-star",
        Description: "Avoid SELECT * in queries.",
        Category: "Performance",
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: false
    );

    public IEnumerable<Diagnostic> Analyze(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var range = FindSelectStarRange(context.Tokens);
        if (range is null)
        {
            yield break;
        }

        yield return RuleHelpers.CreateDiagnostic(
            range: range,
            message: "Avoid SELECT *; explicitly list required columns.",
            code: Metadata.RuleId,
            category: Metadata.Category,
            fixable: Metadata.Fixable
        );
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private static TsqlRefine.PluginSdk.Range? FindSelectStarRange(IReadOnlyList<Token> tokens)
    {
        if (tokens is null || tokens.Count == 0)
        {
            return null;
        }

        for (var i = 0; i < tokens.Count; i++)
        {
            if (!TokenHelpers.IsKeyword(tokens[i], "select"))
            {
                continue;
            }

            // Check if this SELECT is inside an EXISTS clause
            if (IsInsideExists(tokens, i))
            {
                continue;
            }

            var depth = 0;
            for (var j = i + 1; j < tokens.Count; j++)
            {
                if (TokenHelpers.IsTrivia(tokens[j]))
                {
                    continue;
                }

                var text = tokens[j].Text;
                if (TokenHelpers.IsKeyword(tokens[j], "from") || TokenHelpers.IsKeyword(tokens[j], "into"))
                {
                    break;
                }

                if (text == "(")
                {
                    depth++;
                    continue;
                }

                if (text == ")" && depth > 0)
                {
                    depth--;
                    continue;
                }

                if (depth == 0 && text == "*" && !TokenHelpers.IsPrefixedByDot(tokens, j))
                {
                    return TokenHelpers.GetTokenRange(tokens, j, j);
                }
            }
        }

        return null;
    }

    private static bool IsInsideExists(IReadOnlyList<Token> tokens, int selectIndex)
    {
        // Look backwards from SELECT to find EXISTS keyword followed by opening parenthesis
        var parenDepth = 0;
        for (var i = selectIndex - 1; i >= 0; i--)
        {
            if (TokenHelpers.IsTrivia(tokens[i]))
            {
                continue;
            }

            if (tokens[i].Text == ")")
            {
                parenDepth++;
                continue;
            }

            if (tokens[i].Text == "(")
            {
                if (parenDepth == 0)
                {
                    // Found opening paren at our level, check if preceded by EXISTS
                    for (var j = i - 1; j >= 0; j--)
                    {
                        if (TokenHelpers.IsTrivia(tokens[j]))
                        {
                            continue;
                        }

                        if (TokenHelpers.IsKeyword(tokens[j], "exists"))
                        {
                            return true;
                        }

                        // Any other non-whitespace token means this isn't EXISTS
                        break;
                    }

                    return false;
                }

                parenDepth--;
            }
        }

        return false;
    }
}
