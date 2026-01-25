using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers;

namespace TsqlRefine.Rules.Rules;

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

        yield return new Diagnostic(
            Range: range,
            Message: "Avoid SELECT *; explicitly list required columns.",
            Severity: null,
            Code: Metadata.RuleId,
            Data: new DiagnosticData(Metadata.RuleId, Metadata.Category, Metadata.Fixable)
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
                    var start = tokens[j].Start;
                    var end = TokenHelpers.GetTokenEnd(tokens[j]);
                    return new TsqlRefine.PluginSdk.Range(start, end);
                }
            }
        }

        return null;
    }
}
