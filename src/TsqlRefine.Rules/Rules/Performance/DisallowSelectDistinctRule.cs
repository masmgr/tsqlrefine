using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers;

namespace TsqlRefine.Rules.Rules.Performance;

// TODO: Use AST
public sealed class DisallowSelectDistinctRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "disallow-select-distinct",
        Description: "Flags SELECT DISTINCT usage which often masks JOIN bugs or missing GROUP BY, and has performance implications.",
        Category: "Performance",
        DefaultSeverity: RuleSeverity.Information,
        Fixable: false
    );

    public IEnumerable<Diagnostic> Analyze(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        for (var i = 0; i < context.Tokens.Count; i++)
        {
            // Look for SELECT keyword
            if (TokenHelpers.IsKeyword(context.Tokens[i], "SELECT"))
            {
                // Check next non-trivia token for DISTINCT
                var nextIndex = i + 1;
                while (nextIndex < context.Tokens.Count && TokenHelpers.IsTrivia(context.Tokens[nextIndex]))
                {
                    nextIndex++;
                }

                if (nextIndex < context.Tokens.Count && TokenHelpers.IsKeyword(context.Tokens[nextIndex], "DISTINCT"))
                {
                    // Check if this is COUNT(DISTINCT ...) pattern by looking backward
                    var isCountDistinct = false;
                    var prevIndex = i - 1;
                    while (prevIndex >= 0 && TokenHelpers.IsTrivia(context.Tokens[prevIndex]))
                    {
                        prevIndex--;
                    }

                    if (prevIndex >= 0 && context.Tokens[prevIndex].Text == "(")
                    {
                        // Look further back for COUNT keyword
                        var countIndex = prevIndex - 1;
                        while (countIndex >= 0 && TokenHelpers.IsTrivia(context.Tokens[countIndex]))
                        {
                            countIndex--;
                        }

                        if (countIndex >= 0 && TokenHelpers.IsKeyword(context.Tokens[countIndex], "COUNT"))
                        {
                            isCountDistinct = true;
                        }
                    }

                    // Only flag SELECT DISTINCT, not COUNT(DISTINCT ...)
                    if (!isCountDistinct)
                    {
                        var range = TokenHelpers.GetTokenRange(context.Tokens, nextIndex, nextIndex);
                        yield return RuleHelpers.CreateDiagnostic(
                            range: range,
                            message: "SELECT DISTINCT often masks JOIN bugs or missing GROUP BY, and adds implicit sort/hash operations. Consider using GROUP BY or fixing JOIN logic instead.",
                            code: "disallow-select-distinct",
                            category: "Performance",
                            fixable: false
                        );
                    }
                }
            }
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);
}
