using System.Collections.Frozen;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace TsqlRefine.Formatting.Helpers;

/// <summary>
/// Shared helper for ScriptDom token stream operations used by multiple formatting passes.
/// </summary>
internal static class ScriptDomTokenHelper
{
    /// <summary>
    /// Token types considered trivia (whitespace and comments).
    /// </summary>
    public static readonly FrozenSet<TSqlTokenType> TriviaTokenTypes = BuildTriviaTokenTypes();

    /// <summary>
    /// Creates a T-SQL parser for the specified compatibility level.
    /// </summary>
    public static TSqlParser CreateParser(int compatLevel) =>
        compatLevel switch
        {
            >= 160 => new TSql160Parser(initialQuotedIdentifiers: true),
            >= 150 => new TSql150Parser(initialQuotedIdentifiers: true),
            >= 140 => new TSql140Parser(initialQuotedIdentifiers: true),
            >= 130 => new TSql130Parser(initialQuotedIdentifiers: true),
            >= 120 => new TSql120Parser(initialQuotedIdentifiers: true),
            >= 110 => new TSql110Parser(initialQuotedIdentifiers: true),
            _ => new TSql100Parser(initialQuotedIdentifiers: true)
        };

    /// <summary>
    /// Checks if a token is trivia (whitespace or comment).
    /// </summary>
    public static bool IsTrivia(TSqlParserToken token) => TriviaTokenTypes.Contains(token.TokenType);

    /// <summary>
    /// Builds an index array where each element contains the index of the previous non-trivia token.
    /// Returns -1 for tokens with no preceding non-trivia token.
    /// </summary>
    public static int[] BuildPreviousNonTriviaIndexes(IList<TSqlParserToken> tokens)
    {
        var indexes = new int[tokens.Count];
        var previousIndex = -1;

        for (var i = 0; i < tokens.Count; i++)
        {
            indexes[i] = previousIndex;
            if (!IsTrivia(tokens[i]))
            {
                previousIndex = i;
            }
        }

        return indexes;
    }

    /// <summary>
    /// Builds an index array where each element contains the index of the next non-trivia token.
    /// Returns -1 for tokens with no following non-trivia token.
    /// </summary>
    public static int[] BuildNextNonTriviaIndexes(IList<TSqlParserToken> tokens)
    {
        var indexes = new int[tokens.Count];
        var nextIndex = -1;

        for (var i = tokens.Count - 1; i >= 0; i--)
        {
            indexes[i] = nextIndex;
            if (!IsTrivia(tokens[i]))
            {
                nextIndex = i;
            }
        }

        return indexes;
    }

    private static FrozenSet<TSqlTokenType> BuildTriviaTokenTypes()
    {
        var triviaTypes = new HashSet<TSqlTokenType>();
        foreach (var tokenType in Enum.GetValues<TSqlTokenType>())
        {
            var name = tokenType.ToString();
            if (name.Contains("WhiteSpace", StringComparison.Ordinal) ||
                name.Contains("Whitespace", StringComparison.Ordinal) ||
                name.Contains("Comment", StringComparison.Ordinal))
            {
                triviaTypes.Add(tokenType);
            }
        }

        return triviaTypes.ToFrozenSet();
    }
}
