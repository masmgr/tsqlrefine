using System.Collections.Frozen;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Correctness;

/// <summary>Detects function calls passed directly as EXEC procedure arguments.</summary>
public sealed class ExecParameterFunctionCallRule : IRule
{
    private static readonly FrozenSet<string> StatementStartKeywords = new[]
    {
        "ALTER", "BACKUP", "BEGIN", "CHECKPOINT", "COMMIT", "CREATE", "DBCC", "DECLARE",
        "DELETE", "DENY", "DROP", "EXEC", "EXECUTE", "GRANT", "IF", "INSERT", "MERGE",
        "PRINT", "RAISERROR", "RESTORE", "RETURN", "REVOKE", "ROLLBACK", "SAVE", "SELECT",
        "SET", "THROW", "TRUNCATE", "UPDATE", "USE", "WAITFOR", "WHILE"
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    public RuleMetadata Metadata { get; } = new(
        "exec-parameter-function-call",
        "Detects function calls passed directly as EXEC procedure arguments, which SQL Server rejects.",
        "Correctness",
        RuleSeverity.Error,
        false);

    public IEnumerable<Diagnostic> Analyze(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // ScriptDOM rejects direct function-call arguments during parsing, so this rule must
        // inspect tokens while carefully preserving EXEC statement boundaries.
        for (var index = 0; index < context.Tokens.Count; index++)
        {
            if (TokenHelpers.IsTrivia(context.Tokens[index]) ||
                !TokenHelpers.IsKeyword(context.Tokens[index], "EXEC") &&
                !TokenHelpers.IsKeyword(context.Tokens[index], "EXECUTE"))
            {
                continue;
            }

            foreach (var diagnostic in AnalyzeExecute(context.Tokens, index))
            {
                yield return diagnostic;
            }
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private IEnumerable<Diagnostic> AnalyzeExecute(IReadOnlyList<Token> tokens, int executeIndex)
    {
        var procedureIndex = GetProcedureIndex(tokens, executeIndex);
        if (procedureIndex < 0 ||
            TokenHelpers.IsKeyword(tokens[procedureIndex], "AS") ||
            tokens[procedureIndex].Text == "(" ||
            tokens[procedureIndex].Text.StartsWith('@'))
        {
            yield break;
        }

        var argumentIndex = SkipProcedureName(tokens, procedureIndex);
        while (argumentIndex >= 0 && argumentIndex < tokens.Count)
        {
            argumentIndex = TokenHelpers.SkipTrivia(tokens, argumentIndex);
            if (argumentIndex >= tokens.Count || IsStatementEnd(tokens[argumentIndex]))
            {
                yield break;
            }

            var valueIndex = GetArgumentValueIndex(tokens, argumentIndex);
            if (valueIndex < 0)
            {
                yield break;
            }

            if (TryGetFunctionCallRange(tokens, valueIndex, out var endIndex))
            {
                yield return new Diagnostic(
                    TokenHelpers.GetTokenRange(tokens, valueIndex, endIndex),
                    "Function calls cannot be passed directly as EXEC procedure arguments; assign the result to a variable first.",
                    Severity: null,
                    Code: Metadata.RuleId,
                    Data: new DiagnosticData(Metadata.RuleId, Metadata.Category, Metadata.Fixable));
                argumentIndex = TokenHelpers.GetNextNonTriviaIndex(tokens, endIndex);
            }
            else
            {
                argumentIndex = FindNextArgument(tokens, valueIndex);
            }

            if (argumentIndex < 0 || argumentIndex >= tokens.Count || tokens[argumentIndex].Text != ",")
            {
                yield break;
            }

            argumentIndex++;
        }
    }

    private static int GetProcedureIndex(IReadOnlyList<Token> tokens, int executeIndex)
    {
        var procedureIndex = TokenHelpers.GetNextNonTriviaIndex(tokens, executeIndex);
        if (procedureIndex < 0 || !tokens[procedureIndex].Text.StartsWith('@'))
        {
            return procedureIndex;
        }

        var equalsIndex = TokenHelpers.GetNextNonTriviaIndex(tokens, procedureIndex);
        return equalsIndex >= 0 && tokens[equalsIndex].Text == "="
            ? TokenHelpers.GetNextNonTriviaIndex(tokens, equalsIndex)
            : procedureIndex;
    }

    private static int SkipProcedureName(IReadOnlyList<Token> tokens, int index)
    {
        var current = index;
        var separatorCount = 0;
        while (separatorCount < 3)
        {
            var separatorIndex = TokenHelpers.GetNextNonTriviaIndex(tokens, current);
            if (separatorIndex < 0 || tokens[separatorIndex].Text != ".")
            {
                return separatorIndex;
            }

            separatorCount++;
            var nextIndex = TokenHelpers.GetNextNonTriviaIndex(tokens, separatorIndex);
            if (nextIndex < 0)
            {
                return -1;
            }

            if (tokens[nextIndex].Text == ".")
            {
                separatorCount++;
                if (separatorCount > 3)
                {
                    return nextIndex;
                }

                nextIndex = TokenHelpers.GetNextNonTriviaIndex(tokens, nextIndex);
                if (nextIndex < 0)
                {
                    return -1;
                }
            }

            current = nextIndex;
        }

        return TokenHelpers.GetNextNonTriviaIndex(tokens, current);
    }

    private static int GetArgumentValueIndex(IReadOnlyList<Token> tokens, int index)
    {
        if (!tokens[index].Text.StartsWith('@'))
        {
            return index;
        }

        var equalsIndex = TokenHelpers.GetNextNonTriviaIndex(tokens, index);
        if (equalsIndex < 0 || tokens[equalsIndex].Text != "=")
        {
            return index;
        }

        return TokenHelpers.GetNextNonTriviaIndex(tokens, equalsIndex);
    }

    private static bool TryGetFunctionCallRange(
        IReadOnlyList<Token> tokens,
        int startIndex,
        out int endIndex)
    {
        endIndex = -1;
        if (!IsFunctionName(tokens[startIndex]))
        {
            return false;
        }

        var openParenthesisIndex = TokenHelpers.GetNextNonTriviaIndex(tokens, startIndex);
        while (openParenthesisIndex >= 0 && tokens[openParenthesisIndex].Text == ".")
        {
            var functionPartIndex = TokenHelpers.GetNextNonTriviaIndex(tokens, openParenthesisIndex);
            if (functionPartIndex < 0 || !IsFunctionName(tokens[functionPartIndex]))
            {
                return false;
            }

            openParenthesisIndex = TokenHelpers.GetNextNonTriviaIndex(tokens, functionPartIndex);
        }

        if (openParenthesisIndex < 0 || tokens[openParenthesisIndex].Text != "(")
        {
            return false;
        }

        var depth = 0;
        for (var index = openParenthesisIndex; index < tokens.Count; index++)
        {
            if (TokenHelpers.IsTrivia(tokens[index]))
            {
                continue;
            }

            if (tokens[index].Text == "(")
            {
                depth++;
            }
            else if (tokens[index].Text == ")" && --depth == 0)
            {
                endIndex = index;
                return true;
            }
        }

        return false;
    }

    private static int FindNextArgument(IReadOnlyList<Token> tokens, int startIndex)
    {
        var depth = 0;
        for (var index = startIndex; index < tokens.Count; index++)
        {
            if (TokenHelpers.IsTrivia(tokens[index]))
            {
                continue;
            }

            switch (tokens[index].Text)
            {
                case "(":
                    depth++;
                    break;
                case ")":
                    if (depth == 0)
                    {
                        return index;
                    }

                    depth--;
                    break;
                case "," when depth == 0:
                case ";" when depth == 0:
                    return index;
            }

            if (depth == 0 &&
                index > startIndex &&
                (TokenHelpers.IsKeyword(tokens[index], "GO") || StatementStartKeywords.Contains(tokens[index].Text)))
            {
                return index;
            }
        }

        return -1;
    }

    private static bool IsFunctionName(Token token) =>
        !token.Text.StartsWith('@') &&
        token.Text.Length > 0 &&
        (char.IsLetter(token.Text[0]) || token.Text[0] == '[' || token.Text[0] == '_');

    private static bool IsStatementEnd(Token token) =>
        token.Text is ";" or ")" ||
        TokenHelpers.IsKeyword(token, "GO") ||
        StatementStartKeywords.Contains(token.Text);
}
