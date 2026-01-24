using TsqlRefine.PluginSdk;

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

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(diagnostic);
        return Array.Empty<Fix>();
    }

    private static TsqlRefine.PluginSdk.Range? FindSelectStarRange(IReadOnlyList<Token> tokens)
    {
        if (tokens is null || tokens.Count == 0)
        {
            return null;
        }

        for (var i = 0; i < tokens.Count; i++)
        {
            if (!IsKeyword(tokens[i], "select"))
            {
                continue;
            }

            var depth = 0;
            for (var j = i + 1; j < tokens.Count; j++)
            {
                if (IsTrivia(tokens[j]))
                {
                    continue;
                }

                var text = tokens[j].Text;
                if (IsKeyword(tokens[j], "from") || IsKeyword(tokens[j], "into"))
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

                if (depth == 0 && text == "*" && !IsPrefixedByDot(tokens, j))
                {
                    var start = tokens[j].Start;
                    var end = GetTokenEnd(tokens[j]);
                    return new TsqlRefine.PluginSdk.Range(start, end);
                }
            }
        }

        return null;
    }

    private static bool IsKeyword(Token token, string keyword) =>
        token.Text.Equals(keyword, StringComparison.OrdinalIgnoreCase);

    private static bool IsTrivia(Token token)
    {
        var text = token.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        return text.StartsWith("--", StringComparison.Ordinal) || text.StartsWith("/*", StringComparison.Ordinal);
    }

    private static bool IsPrefixedByDot(IReadOnlyList<Token> tokens, int index)
    {
        for (var i = index - 1; i >= 0; i--)
        {
            if (IsTrivia(tokens[i]))
            {
                continue;
            }

            return tokens[i].Text == ".";
        }

        return false;
    }

    private static Position GetTokenEnd(Token token)
    {
        var text = token.Text ?? string.Empty;
        var line = token.Start.Line;
        var character = token.Start.Character;

        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (ch == '\r')
            {
                if (i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }

                line++;
                character = 0;
                continue;
            }

            if (ch == '\n')
            {
                line++;
                character = 0;
                continue;
            }

            character++;
        }

        return new Position(line, character);
    }
}
