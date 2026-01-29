using System.Text;
using TsqlRefine.Core.Model;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Core.Engine;

/// <summary>
/// Applies fixes to SQL text based on diagnostics and rules.
/// </summary>
internal sealed class FixApplier
{
    /// <summary>
    /// Applies fixes from the given diagnostic fix groups to the source text.
    /// </summary>
    public static FixOutcome ApplyFixes(string text, IReadOnlyList<DiagnosticFixGroup> fixGroups)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(fixGroups);

        if (fixGroups.Count == 0)
        {
            return new FixOutcome(text, Array.Empty<AppliedFix>(), Array.Empty<SkippedFix>());
        }

        var lineMap = TextPositionMapper.BuildLineMap(text);
        var orderedGroups = OrderFixGroups(fixGroups);

        var appliedFixes = new List<AppliedFix>();
        var skippedFixes = new List<SkippedFix>();
        var appliedRanges = new List<TextRange>();
        var resolvedEdits = new List<ResolvedEdit>();

        foreach (var group in orderedGroups)
        {
            var ruleId = group.Rule.Metadata.RuleId;
            var applied = false;

            foreach (var fix in group.Fixes)
            {
                if (!TryResolveFix(fix, lineMap, out var resolved, out var reason))
                {
                    skippedFixes.Add(new SkippedFix(ruleId, fix.Title, reason));
                    continue;
                }

                if (HasOverlap(resolved.Edits, appliedRanges))
                {
                    skippedFixes.Add(new SkippedFix(ruleId, fix.Title, "Conflicts with another fix."));
                    continue;
                }

                appliedFixes.Add(new AppliedFix(ruleId, fix.Title, fix.Edits));
                resolvedEdits.AddRange(resolved.Edits);
                appliedRanges.AddRange(resolved.Edits.Select(e => new TextRange(e.Start, e.End)));
                applied = true;
                break;
            }

            if (!applied && group.Fixes.Count == 0)
            {
                skippedFixes.Add(new SkippedFix(ruleId, "(no fix)", "No applicable fixes."));
            }
        }

        if (resolvedEdits.Count == 0)
        {
            return new FixOutcome(text, appliedFixes, skippedFixes);
        }

        var fixedText = ApplyEdits(text, resolvedEdits);
        return new FixOutcome(fixedText, appliedFixes, skippedFixes);
    }

    private static IReadOnlyList<DiagnosticFixGroup> OrderFixGroups(IReadOnlyList<DiagnosticFixGroup> fixGroups)
    {
        return fixGroups
            .OrderBy(g => g.Diagnostic.Range.Start.Line)
            .ThenBy(g => g.Diagnostic.Range.Start.Character)
            .ThenBy(g => g.Diagnostic.Range.End.Line)
            .ThenBy(g => g.Diagnostic.Range.End.Character)
            .ThenBy(g => g.Rule.Metadata.RuleId, StringComparer.Ordinal)
            .ToArray();
    }

    private static string ApplyEdits(string text, IReadOnlyList<ResolvedEdit> edits)
    {
        var ordered = edits
            .OrderByDescending(e => e.Start)
            .ThenByDescending(e => e.End)
            .ToArray();

        var builder = new StringBuilder(text);
        foreach (var edit in ordered)
        {
            builder.Remove(edit.Start, edit.End - edit.Start);
            builder.Insert(edit.Start, edit.NewText);
        }

        return builder.ToString();
    }

    private static bool TryResolveFix(Fix fix, TextPositionMapper.LineMap lineMap, out ResolvedFix resolvedFix, out string reason)
    {
        resolvedFix = default!;
        reason = string.Empty;

        if (fix.Edits is null || fix.Edits.Count == 0)
        {
            reason = "No edits.";
            return false;
        }

        var edits = new List<ResolvedEdit>(fix.Edits.Count);
        foreach (var edit in fix.Edits)
        {
            if (!TextPositionMapper.TryResolveRange(edit.Range, lineMap, out var start, out var end))
            {
                reason = "Invalid edit range.";
                return false;
            }

            if (start > end)
            {
                reason = "Invalid edit range.";
                return false;
            }

            edits.Add(new ResolvedEdit(start, end, edit.NewText ?? string.Empty));
        }

        if (HasInternalOverlap(edits))
        {
            reason = "Overlapping edits in a single fix.";
            return false;
        }

        resolvedFix = new ResolvedFix(edits);
        return true;
    }

    private static bool HasInternalOverlap(IReadOnlyList<ResolvedEdit> edits)
    {
        if (edits.Count <= 1)
        {
            return false;
        }

        var ordered = edits
            .OrderBy(e => e.Start)
            .ThenBy(e => e.End)
            .ToArray();

        for (var i = 1; i < ordered.Length; i++)
        {
            if (RangesOverlap(ordered[i - 1].Start, ordered[i - 1].End, ordered[i].Start, ordered[i].End))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasOverlap(IReadOnlyList<ResolvedEdit> edits, IReadOnlyList<TextRange> appliedRanges)
    {
        foreach (var edit in edits)
        {
            foreach (var range in appliedRanges)
            {
                if (RangesOverlap(edit.Start, edit.End, range.Start, range.End))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool RangesOverlap(int start1, int end1, int start2, int end2)
    {
        if (start1 == end1 && start2 == end2 && start1 == start2)
        {
            return true;
        }

        return start1 < end2 && end1 > start2;
    }

    internal sealed record FixOutcome(
        string FixedText,
        IReadOnlyList<AppliedFix> AppliedFixes,
        IReadOnlyList<SkippedFix> SkippedFixes
    );

    internal sealed record ResolvedFix(IReadOnlyList<ResolvedEdit> Edits);

    internal sealed record ResolvedEdit(int Start, int End, string NewText);

    internal sealed record TextRange(int Start, int End);
}
