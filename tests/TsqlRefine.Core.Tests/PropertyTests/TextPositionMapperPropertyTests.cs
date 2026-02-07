using FsCheck;
using FsCheck.Xunit;
using TsqlRefine.Core.Engine;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Core.Tests.PropertyTests;

/// <summary>
/// Property-based tests for TextPositionMapper: line map invariants,
/// offset resolution correctness, and round-trip consistency.
/// </summary>
public sealed class TextPositionMapperPropertyTests
{
    [Property(MaxTest = 300)]
    public bool LineStarts_AreMonotonicallyIncreasing(NonNull<string> text)
    {
        var map = TextPositionMapper.BuildLineMap(text.Get);
        var starts = map.LineStarts;

        for (var i = 1; i < starts.Count; i++)
        {
            if (starts[i] <= starts[i - 1])
            {
                return false;
            }
        }

        return true;
    }

    [Property(MaxTest = 300)]
    public bool LineStarts_And_LineLengths_HaveSameCount(NonNull<string> text)
    {
        var map = TextPositionMapper.BuildLineMap(text.Get);
        return map.LineStarts.Count == map.LineLengths.Count;
    }

    [Property(MaxTest = 300)]
    public bool FirstLineStart_IsAlwaysZero(NonNull<string> text)
    {
        var map = TextPositionMapper.BuildLineMap(text.Get);
        return map.LineStarts[0] == 0;
    }

    [Property(MaxTest = 300)]
    public bool AllValidPositions_AreResolvable(NonNull<string> text)
    {
        var map = TextPositionMapper.BuildLineMap(text.Get);

        for (var line = 0; line < map.LineLengths.Count; line++)
        {
            for (var ch = 0; ch <= map.LineLengths[line]; ch++)
            {
                var pos = new Position(line, ch);
                if (!TextPositionMapper.TryGetOffset(map, pos, out _))
                {
                    return false;
                }
            }
        }

        return true;
    }

    [Property(MaxTest = 300)]
    public bool InvalidLine_IsNotResolvable(NonNull<string> text)
    {
        var map = TextPositionMapper.BuildLineMap(text.Get);
        var invalidLine = map.LineStarts.Count;
        var pos = new Position(invalidLine, 0);
        return !TextPositionMapper.TryGetOffset(map, pos, out _);
    }

    [Property(MaxTest = 300)]
    public bool NegativePosition_IsNotResolvable(NegativeInt line, NegativeInt ch)
    {
        var map = TextPositionMapper.BuildLineMap("hello\nworld");
        var pos = new Position(line.Get, ch.Get);
        return !TextPositionMapper.TryGetOffset(map, pos, out _);
    }

    [Property(MaxTest = 200)]
    public bool ResolvedOffset_IsWithinTextBounds(NonNull<string> text)
    {
        var map = TextPositionMapper.BuildLineMap(text.Get);

        for (var line = 0; line < map.LineLengths.Count; line++)
        {
            for (var ch = 0; ch <= map.LineLengths[line]; ch++)
            {
                if (TextPositionMapper.TryGetOffset(map, new Position(line, ch), out var offset))
                {
                    if (offset < 0 || offset > text.Get.Length)
                    {
                        return false;
                    }
                }
            }
        }

        return true;
    }

    [Property(MaxTest = 200)]
    public bool TryResolveRange_FullRange_Succeeds(NonNull<string> text)
    {
        var map = TextPositionMapper.BuildLineMap(text.Get);
        if (map.LineLengths.Count == 0)
        {
            return true;
        }

        var start = new Position(0, 0);
        var lastLine = map.LineLengths.Count - 1;
        var lastChar = map.LineLengths[lastLine];
        var end = new Position(lastLine, lastChar);
        var range = new TsqlRefine.PluginSdk.Range(start, end);

        return TextPositionMapper.TryResolveRange(range, map, out var s, out var e)
               && s == 0
               && e <= text.Get.Length;
    }
}
