using TsqlRefine.PluginSdk;

namespace TsqlRefine.Core.Engine;

/// <summary>
/// Maps between line/character positions and text offsets.
/// </summary>
internal static class TextPositionMapper
{
    /// <summary>
    /// Builds a line map from the given text.
    /// </summary>
    public static LineMap BuildLineMap(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var lineStarts = new List<int> { 0 };
        var lineLengths = new List<int>();
        var currentLength = 0;

        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (ch == '\r' || ch == '\n')
            {
                lineLengths.Add(currentLength);
                currentLength = 0;

                if (ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }

                lineStarts.Add(i + 1);
                continue;
            }

            currentLength++;
        }

        lineLengths.Add(currentLength);
        return new LineMap(lineStarts, lineLengths);
    }

    /// <summary>
    /// Attempts to resolve a range to start and end offsets.
    /// </summary>
    public static bool TryResolveRange(TsqlRefine.PluginSdk.Range range, LineMap lineMap, out int start, out int end)
    {
        start = 0;
        end = 0;

        if (!TryGetOffset(lineMap, range.Start, out start))
        {
            return false;
        }

        if (!TryGetOffset(lineMap, range.End, out end))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Attempts to get the text offset for a given position.
    /// </summary>
    public static bool TryGetOffset(LineMap lineMap, Position position, out int offset)
    {
        offset = 0;
        if (position.Line < 0 || position.Character < 0)
        {
            return false;
        }

        if (position.Line >= lineMap.LineStarts.Count)
        {
            return false;
        }

        var lineLength = lineMap.LineLengths[position.Line];
        if (position.Character > lineLength)
        {
            return false;
        }

        offset = lineMap.LineStarts[position.Line] + position.Character;
        return true;
    }

    /// <summary>
    /// Represents a mapping of line starts and lengths in a text document.
    /// </summary>
    internal sealed record LineMap(IReadOnlyList<int> LineStarts, IReadOnlyList<int> LineLengths);
}
