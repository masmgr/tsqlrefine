using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Helpers.Diagnostics;

/// <summary>
/// Helper utilities for working with Microsoft ScriptDom fragments and tokens.
/// </summary>
public static class ScriptDomHelpers
{
    /// <summary>
    /// Returns whether the given table name refers to a temporary table (local # or global ##).
    /// </summary>
    /// <param name="tableName">The table name to check (typically from BaseIdentifier.Value).</param>
    /// <returns>True if the name starts with '#', indicating a temporary table.</returns>
    public static bool IsTemporaryTableName(string? tableName) =>
        tableName is not null && tableName.StartsWith('#');

    /// <summary>
    /// Returns a range covering only the first token of the fragment.
    /// Useful for narrowing diagnostics to the statement keyword (e.g. UPDATE, DELETE, MERGE).
    /// </summary>
    /// <param name="fragment">The TSqlFragment to get the first token range from.</param>
    /// <returns>A Range covering only the first token, or the full fragment range as fallback.</returns>
    /// <exception cref="ArgumentNullException">Thrown when fragment is null.</exception>
    public static TsqlRefine.PluginSdk.Range GetFirstTokenRange(TSqlFragment fragment)
    {
        ArgumentNullException.ThrowIfNull(fragment);

        if (fragment.ScriptTokenStream != null &&
            fragment.FirstTokenIndex >= 0 &&
            fragment.FirstTokenIndex < fragment.ScriptTokenStream.Count)
        {
            var firstToken = fragment.ScriptTokenStream[fragment.FirstTokenIndex];
            var tokenText = firstToken.Text ?? string.Empty;
            var start = new Position(firstToken.Line - 1, firstToken.Column - 1);
            var end = new Position(firstToken.Line - 1, firstToken.Column - 1 + tokenText.Length);
            return new TsqlRefine.PluginSdk.Range(start, end);
        }

        return GetRange(fragment);
    }

    /// <summary>
    /// Searches for a token with the specified type within the fragment's token range.
    /// Falls back to <see cref="GetFirstTokenRange"/> if the token is not found.
    /// </summary>
    /// <param name="fragment">The TSqlFragment to search within.</param>
    /// <param name="tokenType">The TSqlTokenType to find.</param>
    /// <returns>A Range covering only the matched token, or the first token range as fallback.</returns>
    /// <exception cref="ArgumentNullException">Thrown when fragment is null.</exception>
    public static TsqlRefine.PluginSdk.Range FindKeywordTokenRange(TSqlFragment fragment, TSqlTokenType tokenType)
    {
        ArgumentNullException.ThrowIfNull(fragment);

        if (fragment.ScriptTokenStream != null)
        {
            for (var i = fragment.FirstTokenIndex; i <= fragment.LastTokenIndex && i < fragment.ScriptTokenStream.Count; i++)
            {
                var token = fragment.ScriptTokenStream[i];
                if (token.TokenType == tokenType)
                {
                    var text = token.Text ?? string.Empty;
                    var start = new Position(token.Line - 1, token.Column - 1);
                    var end = new Position(token.Line - 1, token.Column - 1 + text.Length);
                    return new TsqlRefine.PluginSdk.Range(start, end);
                }
            }
        }

        return GetFirstTokenRange(fragment);
    }

    /// <summary>
    /// Converts a TSqlFragment to a Range using its start/end token positions.
    /// Handles 1-based ScriptDom coordinates and converts to 0-based PluginSdk positions.
    /// </summary>
    /// <param name="fragment">The TSqlFragment to convert.</param>
    /// <returns>A Range representing the fragment's location in the source code.</returns>
    /// <exception cref="ArgumentNullException">Thrown when fragment is null.</exception>
    public static TsqlRefine.PluginSdk.Range GetRange(TSqlFragment fragment)
    {
        ArgumentNullException.ThrowIfNull(fragment);

        var start = new Position(fragment.StartLine - 1, fragment.StartColumn - 1);
        var end = start;

        // Try to get the end position from the last token
        if (fragment.ScriptTokenStream != null &&
            fragment.LastTokenIndex >= 0 &&
            fragment.LastTokenIndex < fragment.ScriptTokenStream.Count)
        {
            var lastToken = fragment.ScriptTokenStream[fragment.LastTokenIndex];
            var tokenText = lastToken.Text ?? string.Empty;
            end = new Position(
                lastToken.Line - 1,
                lastToken.Column - 1 + tokenText.Length
            );
        }

        return new TsqlRefine.PluginSdk.Range(start, end);
    }
}
