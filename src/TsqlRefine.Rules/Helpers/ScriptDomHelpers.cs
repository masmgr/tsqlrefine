using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Helpers;

/// <summary>
/// Helper utilities for working with Microsoft ScriptDom fragments and tokens.
/// </summary>
public static class ScriptDomHelpers
{
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
