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
    /// Returns a range covering the IN keyword of an InPredicate node.
    /// For NOT IN, returns the range spanning from NOT to IN (inclusive).
    /// For IN alone, returns the range of only the IN token.
    /// </summary>
    /// <param name="node">The InPredicate node.</param>
    /// <returns>A Range covering "IN" or "NOT IN".</returns>
    /// <exception cref="ArgumentNullException">Thrown when node is null.</exception>
    public static TsqlRefine.PluginSdk.Range GetInKeywordRange(InPredicate node)
    {
        ArgumentNullException.ThrowIfNull(node);

        var tokens = node.ScriptTokenStream;
        if (tokens is null)
        {
            return GetRange(node);
        }

        // Find the IN token within the node's token range
        var inIndex = -1;
        for (var i = node.FirstTokenIndex; i <= node.LastTokenIndex && i < tokens.Count; i++)
        {
            if (tokens[i].Text.Equals("IN", StringComparison.OrdinalIgnoreCase))
            {
                inIndex = i;
                break;
            }
        }

        if (inIndex < 0)
        {
            return GetFirstTokenRange(node);
        }

        var startIndex = inIndex;

        // For NOT IN, find the preceding NOT token
        if (node.NotDefined)
        {
            for (var i = inIndex - 1; i >= node.FirstTokenIndex; i--)
            {
                var tokenText = tokens[i].Text;
                if (string.IsNullOrWhiteSpace(tokenText) ||
                    tokenText.StartsWith("--", StringComparison.Ordinal) ||
                    tokenText.StartsWith("/*", StringComparison.Ordinal))
                {
                    continue;
                }

                if (tokenText.Equals("NOT", StringComparison.OrdinalIgnoreCase))
                {
                    startIndex = i;
                }

                break;
            }
        }

        var startToken = tokens[startIndex];
        var endToken = tokens[inIndex];
        var endText = endToken.Text ?? string.Empty;
        var start = new Position(startToken.Line - 1, startToken.Column - 1);
        var end = new Position(endToken.Line - 1, endToken.Column - 1 + endText.Length);
        return new TsqlRefine.PluginSdk.Range(start, end);
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
