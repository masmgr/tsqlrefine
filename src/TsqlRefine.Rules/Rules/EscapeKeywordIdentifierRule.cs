using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers;

namespace TsqlRefine.Rules.Rules;

public sealed class EscapeKeywordIdentifierRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "escape-keyword-identifier",
        Description: "Warns when a Transact-SQL keyword is used as a table/column identifier without escaping, and offers an autofix to bracket it.",
        Category: "Correctness",
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

        foreach (var keywordToken in FindKeywordIdentifierTokens(tokens))
        {
            var start = keywordToken.Start;
            var end = TokenHelpers.GetTokenEnd(keywordToken);
            var escaped = $"[{keywordToken.Text}]";

            yield return new Diagnostic(
                Range: new TsqlRefine.PluginSdk.Range(start, end),
                Message: $"Identifier '{keywordToken.Text}' is a Transact-SQL keyword. Escape it as {escaped} when used as a table or column name.",
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

        var token = FindTokenByRange(context.Tokens, diagnostic.Range);
        if (token is null)
        {
            yield break;
        }

        yield return new Fix(
            Title: "Escape keyword identifier",
            Edits: new[] { new TextEdit(diagnostic.Range, $"[{token.Text}]") }
        );
    }

    private static IEnumerable<Token> FindKeywordIdentifierTokens(IReadOnlyList<Token> tokens)
    {
        var parenDepth = 0;

        var sawCreate = false;
        var sawCreateTable = false;
        var createTableParenDepth = -1;
        var inCreateTableColumnList = false;
        var expectingCreateTableItemStart = false;

        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            if (TokenHelpers.IsTrivia(token))
            {
                continue;
            }

            if (sawCreate && TokenHelpers.IsKeyword(token, "TABLE"))
            {
                sawCreateTable = true;
                sawCreate = false;
            }
            else if (TokenHelpers.IsKeyword(token, "CREATE"))
            {
                sawCreate = true;
                sawCreateTable = false;
            }

            if (token.Text == "(")
            {
                parenDepth++;

                if (sawCreateTable && !inCreateTableColumnList)
                {
                    inCreateTableColumnList = true;
                    createTableParenDepth = parenDepth;
                    expectingCreateTableItemStart = true;
                    sawCreateTable = false;
                }

                continue;
            }

            if (token.Text == ")")
            {
                parenDepth = Math.Max(0, parenDepth - 1);

                if (inCreateTableColumnList && parenDepth < createTableParenDepth)
                {
                    inCreateTableColumnList = false;
                    createTableParenDepth = -1;
                    expectingCreateTableItemStart = false;
                }

                continue;
            }

            if (inCreateTableColumnList && parenDepth == createTableParenDepth)
            {
                if (token.Text == ",")
                {
                    expectingCreateTableItemStart = true;
                    continue;
                }

                if (expectingCreateTableItemStart)
                {
                    expectingCreateTableItemStart = false;

                    if (IsKeywordIdentifierCandidate(token) &&
                        !LooksLikeCreateTableConstraintStart(tokens, i))
                    {
                        yield return token;
                    }

                    continue;
                }
            }

            if (!IsKeywordIdentifierCandidate(token))
            {
                continue;
            }

            // Avoid flagging keywords used as function/procedure names (e.g. OPENJSON(...))
            if (IsFollowedByOpenParen(tokens, i))
            {
                continue;
            }

            if (TokenHelpers.IsPrefixedByDot(tokens, i))
            {
                yield return token;
                continue;
            }

            var previousIndex = GetPreviousNonTriviaIndex(tokens, i);
            if (previousIndex < 0)
            {
                continue;
            }

            if (IsTableNameContextKeyword(tokens[previousIndex]))
            {
                yield return token;
            }
        }
    }

    private static bool IsKeywordIdentifierCandidate(Token token)
    {
        if (!TokenHelpers.IsLikelyKeyword(token))
        {
            return false;
        }

        var text = token.Text;
        return !string.IsNullOrEmpty(text) &&
               char.IsLetter(text[0]);
    }

    private static bool IsFollowedByOpenParen(IReadOnlyList<Token> tokens, int index)
    {
        var nextIndex = GetNextNonTriviaIndex(tokens, index);
        return nextIndex >= 0 && tokens[nextIndex].Text == "(";
    }

    private static bool IsTableNameContextKeyword(Token token) =>
        TokenHelpers.IsKeyword(token, "FROM") ||
        TokenHelpers.IsKeyword(token, "JOIN") ||
        TokenHelpers.IsKeyword(token, "UPDATE") ||
        TokenHelpers.IsKeyword(token, "INTO") ||
        TokenHelpers.IsKeyword(token, "INSERT") ||
        TokenHelpers.IsKeyword(token, "MERGE") ||
        TokenHelpers.IsKeyword(token, "TABLE");

    private static bool LooksLikeCreateTableConstraintStart(IReadOnlyList<Token> tokens, int itemStartIndex)
    {
        var first = tokens[itemStartIndex];
        if (TokenHelpers.IsKeyword(first, "CONSTRAINT"))
        {
            return true;
        }

        var nextIndex = GetNextNonTriviaIndex(tokens, itemStartIndex);
        if (nextIndex < 0)
        {
            return false;
        }

        var second = tokens[nextIndex];

        if (TokenHelpers.IsKeyword(first, "PRIMARY") && TokenHelpers.IsKeyword(second, "KEY"))
        {
            return true;
        }

        if (TokenHelpers.IsKeyword(first, "FOREIGN") && TokenHelpers.IsKeyword(second, "KEY"))
        {
            return true;
        }

        if (TokenHelpers.IsKeyword(first, "CHECK") && second.Text == "(")
        {
            return true;
        }

        if (TokenHelpers.IsKeyword(first, "UNIQUE") &&
            (TokenHelpers.IsKeyword(second, "CLUSTERED") ||
             TokenHelpers.IsKeyword(second, "NONCLUSTERED") ||
             TokenHelpers.IsKeyword(second, "KEY") ||
             second.Text == "("))
        {
            return true;
        }

        return false;
    }

    private static Token? FindTokenByRange(IReadOnlyList<Token> tokens, TsqlRefine.PluginSdk.Range range)
    {
        foreach (var token in tokens)
        {
            if (TokenHelpers.IsTrivia(token))
            {
                continue;
            }

            if (token.Start != range.Start)
            {
                continue;
            }

            var end = TokenHelpers.GetTokenEnd(token);
            if (end == range.End)
            {
                return token;
            }
        }

        return null;
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

    private static int GetNextNonTriviaIndex(IReadOnlyList<Token> tokens, int index)
    {
        for (var i = index + 1; i < tokens.Count; i++)
        {
            if (!TokenHelpers.IsTrivia(tokens[i]))
            {
                return i;
            }
        }

        return -1;
    }
}

