using FsCheck;
using FsCheck.Xunit;
using TsqlRefine.Core.Engine;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Core.Tests.PropertyTests;

/// <summary>
/// Property-based tests for DisableDirectiveParser: suppression correctness,
/// disable/enable pairing, rule scope isolation, and CountLines consistency.
/// </summary>
public sealed class DisableDirectivePropertyTests
{
    [Property(MaxTest = 300)]
    public bool DisabledRange_Suppresses_DiagnosticsWithinRange(
        PositiveInt startLine, PositiveInt rangeSize)
    {
        var start = startLine.Get;
        var end = start + rangeSize.Get;

        var ranges = new[] { new DisabledRange(null, start, end) };
        var midLine = start + rangeSize.Get / 2;
        var diagnostic = CreateDiagnostic(midLine, "test-rule");

        return DisableDirectiveParser.IsSuppressed(diagnostic, ranges);
    }

    [Property(MaxTest = 300)]
    public bool OutsideRange_IsNotSuppressed(
        PositiveInt startLine, PositiveInt rangeSize, PositiveInt offset)
    {
        var start = startLine.Get;
        var end = start + rangeSize.Get;
        var outsideLine = end + offset.Get;

        var ranges = new[] { new DisabledRange(null, start, end) };
        var diagnostic = CreateDiagnostic(outsideLine, "test-rule");

        return !DisableDirectiveParser.IsSuppressed(diagnostic, ranges);
    }

    [Property(MaxTest = 300)]
    public bool RuleSpecificDisable_DoesNotAffectOtherRules(
        PositiveInt startLine, PositiveInt rangeSize)
    {
        var start = startLine.Get;
        var end = start + rangeSize.Get;

        var ranges = new[] { new DisabledRange("rule-a", start, end) };
        var midLine = start + rangeSize.Get / 2;

        var diagnosticA = CreateDiagnostic(midLine, "rule-a");
        var diagnosticB = CreateDiagnostic(midLine, "rule-b");

        return DisableDirectiveParser.IsSuppressed(diagnosticA, ranges)
               && !DisableDirectiveParser.IsSuppressed(diagnosticB, ranges);
    }

    [Theory]
    [InlineData("rule-a")]
    [InlineData("rule-b")]
    [InlineData("avoid-select-star")]
    [InlineData("require-semicolons")]
    public void GlobalDisable_SuppressesAnyRule(string ruleId)
    {
        var ranges = new[] { new DisabledRange(null, 0, 100) };
        var diagnostic = CreateDiagnostic(50, ruleId);

        Assert.True(DisableDirectiveParser.IsSuppressed(diagnostic, ranges));
    }

    [Property(MaxTest = 200)]
    public bool OpenEndedRange_SuppressesToEndOfFile(PositiveInt line)
    {
        var ranges = new[] { new DisabledRange(null, 1, null) };
        var diagnostic = CreateDiagnostic(line.Get, "any-rule");

        return DisableDirectiveParser.IsSuppressed(diagnostic, ranges);
    }

    [Property(MaxTest = 200)]
    public bool EmptyRanges_NeverSuppress(PositiveInt line)
    {
        var ranges = Array.Empty<DisabledRange>();
        var diagnostic = CreateDiagnostic(line.Get, "any-rule");

        return !DisableDirectiveParser.IsSuppressed(diagnostic, ranges);
    }

    [Property(MaxTest = 300)]
    public bool CountLines_MatchesNewlineCount(NonNull<string> text)
    {
        var lineCount = DisableDirectiveParser.CountLines(text.Get);

        if (string.IsNullOrEmpty(text.Get))
        {
            return lineCount == 0;
        }

        // Count newlines manually: non-empty text has at least 1 line
        var expected = 1;
        for (var i = 0; i < text.Get.Length; i++)
        {
            if (text.Get[i] == '\r')
            {
                expected++;
                if (i + 1 < text.Get.Length && text.Get[i + 1] == '\n')
                {
                    i++;
                }
            }
            else if (text.Get[i] == '\n')
            {
                expected++;
            }
        }

        return lineCount == expected;
    }

    [Fact]
    public void CountLines_EmptyString_ReturnsZero()
    {
        Assert.Equal(0, DisableDirectiveParser.CountLines(""));
    }

    [Property(MaxTest = 200)]
    public bool BuildDisabledRanges_MatchedPairs_ProduceClosedRanges(
        PositiveInt startLine, PositiveInt gap)
    {
        var start = startLine.Get % 50 + 1;
        var end = start + (gap.Get % 20) + 1;
        var ruleId = "rule-a";

        var directives = new[]
        {
            new DisableDirective(DisableDirectiveType.Disable, new[] { ruleId }, start),
            new DisableDirective(DisableDirectiveType.Enable, new[] { ruleId }, end),
        };

        var ranges = DisableDirectiveParser.BuildDisabledRanges(directives, end + 10);

        return ranges.Count == 1
               && ranges[0].StartLine == start
               && ranges[0].EndLine == end
               && ranges[0].RuleId == ruleId;
    }

    [Property(MaxTest = 200)]
    public bool BuildDisabledRanges_UnmatchedDisable_ProducesOpenRange(PositiveInt startLine)
    {
        var start = startLine.Get % 50 + 1;

        var directives = new[]
        {
            new DisableDirective(DisableDirectiveType.Disable, Array.Empty<string>(), start),
        };

        var ranges = DisableDirectiveParser.BuildDisabledRanges(directives, 100);

        return ranges.Count == 1 && ranges[0].EndLine is null;
    }

    private static Diagnostic CreateDiagnostic(int line, string ruleId)
    {
        return new Diagnostic(
            new TsqlRefine.PluginSdk.Range(new Position(line, 0), new Position(line, 1)),
            "test message",
            Code: ruleId);
    }
}
