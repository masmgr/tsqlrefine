using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers;

namespace TsqlRefine.Rules.Rules.Style;

public sealed class RequireExplicitJoinTypeRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "require-explicit-join-type",
        Description: "Disallows ambiguous JOIN shorthand; makes JOIN semantics explicit and consistent across a codebase.",
        Category: "Style",
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: true
    );

    public IEnumerable<Diagnostic> Analyze(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var tokens = context.Tokens;
        if (tokens is null || tokens.Count == 0)
        {
            yield break;
        }

        foreach (var joinTokenIndex in FindAmbiguousJoinKeywords(tokens))
        {
            var joinToken = tokens[joinTokenIndex];
            var start = joinToken.Start;
            var end = TokenHelpers.GetTokenEnd(joinToken);

            yield return new Diagnostic(
                Range: new TsqlRefine.PluginSdk.Range(start, end),
                Message: "JOIN must be explicit: use INNER JOIN, LEFT OUTER JOIN, RIGHT OUTER JOIN, or FULL OUTER JOIN.",
                Severity: null,
                Code: Metadata.RuleId,
                Data: new DiagnosticData(Metadata.RuleId, Metadata.Category, Metadata.Fixable)
            );
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(diagnostic);

        var tokens = context.Tokens;
        if (tokens is null || tokens.Count == 0)
        {
            yield break;
        }

        var joinTokenIndex = FindJoinTokenIndexByRange(tokens, diagnostic.Range);
        if (joinTokenIndex < 0)
        {
            yield break;
        }

        if (!TryBuildFix(tokens, joinTokenIndex, out var title, out var editRange, out var newText))
        {
            yield break;
        }

        yield return new Fix(
            Title: title,
            Edits: new[] { new TextEdit(editRange, newText) }
        );
    }

    private static IEnumerable<int> FindAmbiguousJoinKeywords(IReadOnlyList<Token> tokens)
    {
        for (var i = 0; i < tokens.Count; i++)
        {
            if (TokenHelpers.IsTrivia(tokens[i]))
            {
                continue;
            }

            if (!TokenHelpers.IsKeyword(tokens[i], "JOIN"))
            {
                continue;
            }

            if (IsExplicitJoin(tokens, i))
            {
                continue;
            }

            yield return i;
        }
    }

    private static bool IsExplicitJoin(IReadOnlyList<Token> tokens, int joinTokenIndex)
    {
        var previousIndex = GetPreviousNonTriviaIndex(tokens, joinTokenIndex);
        if (previousIndex < 0)
        {
            return true;
        }

        // CROSS JOIN is already explicit and does not take INNER/OUTER.
        if (TokenHelpers.IsKeyword(tokens[previousIndex], "CROSS"))
        {
            return true;
        }

        // INNER JOIN is explicit.
        if (TokenHelpers.IsKeyword(tokens[previousIndex], "INNER"))
        {
            return true;
        }

        // JOIN hints can appear immediately before JOIN, e.g. "LEFT HASH JOIN".
        // In that case, inspect tokens before the hint.
        if (IsJoinHint(tokens[previousIndex]))
        {
            previousIndex = GetPreviousNonTriviaIndex(tokens, previousIndex);
            if (previousIndex < 0)
            {
                return true;
            }
        }

        // OUTER may appear before JOIN (and before a hint), e.g. "LEFT OUTER JOIN" or "LEFT OUTER HASH JOIN".
        if (TokenHelpers.IsKeyword(tokens[previousIndex], "OUTER"))
        {
            previousIndex = GetPreviousNonTriviaIndex(tokens, previousIndex);
            if (previousIndex < 0)
            {
                return true;
            }

            // If OUTER is present with LEFT/RIGHT/FULL, it's explicit.
            return TokenHelpers.IsKeyword(tokens[previousIndex], "LEFT") ||
                   TokenHelpers.IsKeyword(tokens[previousIndex], "RIGHT") ||
                   TokenHelpers.IsKeyword(tokens[previousIndex], "FULL");
        }

        // LEFT/RIGHT/FULL without OUTER are implicit (violations).
        if (TokenHelpers.IsKeyword(tokens[previousIndex], "LEFT") ||
            TokenHelpers.IsKeyword(tokens[previousIndex], "RIGHT") ||
            TokenHelpers.IsKeyword(tokens[previousIndex], "FULL"))
        {
            return false;
        }

        // Otherwise "JOIN" without INNER is implicit INNER (violation).
        return false;
    }

    private static bool TryBuildFix(
        IReadOnlyList<Token> tokens,
        int joinTokenIndex,
        out string title,
        out TsqlRefine.PluginSdk.Range editRange,
        out string newText)
    {
        title = string.Empty;
        editRange = new TsqlRefine.PluginSdk.Range(new Position(0, 0), new Position(0, 0));
        newText = string.Empty;

        var joinToken = tokens[joinTokenIndex];
        var previousIndex = GetPreviousNonTriviaIndex(tokens, joinTokenIndex);
        if (previousIndex < 0)
        {
            return false;
        }

        var joinHintIndex = -1;
        if (IsJoinHint(tokens[previousIndex]))
        {
            joinHintIndex = previousIndex;
            previousIndex = GetPreviousNonTriviaIndex(tokens, previousIndex);
            if (previousIndex < 0)
            {
                return false;
            }
        }

        var hasOuterKeyword = false;
        if (TokenHelpers.IsKeyword(tokens[previousIndex], "OUTER"))
        {
            hasOuterKeyword = true;
            previousIndex = GetPreviousNonTriviaIndex(tokens, previousIndex);
            if (previousIndex < 0)
            {
                return false;
            }
        }

        if (!hasOuterKeyword &&
            (TokenHelpers.IsKeyword(tokens[previousIndex], "LEFT") ||
             TokenHelpers.IsKeyword(tokens[previousIndex], "RIGHT") ||
             TokenHelpers.IsKeyword(tokens[previousIndex], "FULL")))
        {
            var joinType = tokens[previousIndex].Text.ToUpperInvariant();
            title = $"Use {joinType} OUTER JOIN";
            var insertAt = joinHintIndex >= 0 ? tokens[joinHintIndex].Start : joinToken.Start;
            editRange = new TsqlRefine.PluginSdk.Range(insertAt, insertAt);
            newText = "OUTER ";
            return true;
        }

        // Implicit INNER JOIN ("JOIN") must become explicit ("INNER JOIN").
        title = "Use INNER JOIN";
        var insertInnerAt = joinHintIndex >= 0 ? tokens[joinHintIndex].Start : joinToken.Start;
        editRange = new TsqlRefine.PluginSdk.Range(insertInnerAt, insertInnerAt);
        newText = "INNER ";
        return true;
    }

    private static bool IsJoinHint(Token token) =>
        TokenHelpers.IsKeyword(token, "HASH") ||
        TokenHelpers.IsKeyword(token, "LOOP") ||
        TokenHelpers.IsKeyword(token, "MERGE") ||
        TokenHelpers.IsKeyword(token, "REMOTE");

    private static int FindJoinTokenIndexByRange(IReadOnlyList<Token> tokens, TsqlRefine.PluginSdk.Range range)
    {
        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            if (TokenHelpers.IsTrivia(token))
            {
                continue;
            }

            if (token.Start != range.Start)
            {
                continue;
            }

            var end = TokenHelpers.GetTokenEnd(token);
            if (end == range.End && TokenHelpers.IsKeyword(token, "JOIN"))
            {
                return i;
            }
        }

        return -1;
    }

    private static int GetPreviousNonTriviaIndex(IReadOnlyList<Token> tokens, int index)
    {
        for (var i = index - 1; i >= 0; i--)
        {
            if (!TokenHelpers.IsTrivia(tokens[i]))
            {
                return i;
            }
        }

        return -1;
    }
}
