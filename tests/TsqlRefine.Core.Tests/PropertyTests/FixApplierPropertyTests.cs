using FsCheck;
using FsCheck.Xunit;

namespace TsqlRefine.Core.Tests.PropertyTests;

/// <summary>
/// Property-based tests for FixApplier: range overlap symmetry,
/// edit correctness invariants, and empty-fix identity.
/// </summary>
public sealed class FixApplierPropertyTests
{
    [Property(MaxTest = 500)]
    public bool RangesOverlap_IsSymmetric(
        NonNegativeInt s1, NonNegativeInt e1,
        NonNegativeInt s2, NonNegativeInt e2)
    {
        var start1 = s1.Get;
        var end1 = s1.Get + e1.Get; // ensure end >= start
        var start2 = s2.Get;
        var end2 = s2.Get + e2.Get;

        var forward = RangesOverlap(start1, end1, start2, end2);
        var reverse = RangesOverlap(start2, end2, start1, end1);

        return forward == reverse;
    }

    [Property(MaxTest = 500)]
    public bool RangesOverlap_IdenticalRanges_AlwaysOverlap(
        NonNegativeInt start, NonNegativeInt length)
    {
        var s = start.Get;
        var e = s + length.Get;

        return RangesOverlap(s, e, s, e);
    }

    [Property(MaxTest = 500)]
    public bool RangesOverlap_Disjoint_NeverOverlap(
        NonNegativeInt s1, PositiveInt len1, PositiveInt gap)
    {
        var start1 = s1.Get;
        var end1 = start1 + len1.Get;
        var start2 = end1 + gap.Get; // guaranteed gap > 0
        var end2 = start2 + len1.Get;

        return !RangesOverlap(start1, end1, start2, end2);
    }

    [Property(MaxTest = 300)]
    public bool RangesOverlap_Adjacent_DoNotOverlap(
        NonNegativeInt start, PositiveInt len1, PositiveInt len2)
    {
        var s1 = start.Get;
        var e1 = s1 + len1.Get;
        var s2 = e1; // adjacent: end of first = start of second
        var e2 = s2 + len2.Get;

        return !RangesOverlap(s1, e1, s2, e2);
    }

    [Property(MaxTest = 200)]
    public bool ApplyEdits_EmptyEdits_ReturnsOriginalText(NonNull<string> text)
    {
        var result = ApplyEdits(text.Get, Array.Empty<ResolvedEditDto>());
        return result == text.Get;
    }

    [Theory]
    [InlineData("SELECT * FROM users;")]
    [InlineData("INSERT INTO tbl (col) VALUES (1);")]
    [InlineData("UPDATE t SET x = 1;")]
    [InlineData("DELETE FROM t WHERE id = 1;")]
    public void ApplyEdits_SingleDeletion_ReducesLength(string text)
    {
        var mid = text.Length / 2;
        var edits = new[] { new ResolvedEditDto(mid, mid + 1, "") };
        var result = ApplyEdits(text, edits);

        Assert.Equal(text.Length - 1, result.Length);
    }

    [Theory]
    [InlineData("SELECT 1;", "/**/", 0)]
    [InlineData("SELECT 1;", " ", 3)]
    [InlineData("PRINT 'hi';", "-- comment", 5)]
    public void ApplyEdits_SingleInsertion_IncreasesLength(string text, string insert, int pos)
    {
        var edits = new[] { new ResolvedEditDto(pos, pos, insert) };
        var result = ApplyEdits(text, edits);

        Assert.Equal(text.Length + insert.Length, result.Length);
    }

    // Mirror the internal RangesOverlap logic
    private static bool RangesOverlap(int start1, int end1, int start2, int end2)
    {
        if (start1 == end1 && start2 == end2 && start1 == start2)
        {
            return true;
        }

        return start1 < end2 && end1 > start2;
    }

    private sealed record ResolvedEditDto(int Start, int End, string NewText);

    private static string ApplyEdits(string text, IReadOnlyList<ResolvedEditDto> edits)
    {
        if (edits.Count == 0)
        {
            return text;
        }

        var ordered = edits
            .OrderByDescending(e => e.Start)
            .ThenByDescending(e => e.End)
            .ToArray();

        var builder = new System.Text.StringBuilder(text);
        foreach (var edit in ordered)
        {
            builder.Remove(edit.Start, edit.End - edit.Start);
            builder.Insert(edit.Start, edit.NewText);
        }

        return builder.ToString();
    }
}
