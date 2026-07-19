using TsqlRefine.PluginSdk;

namespace TsqlRefine.Qa.Tests;

/// <summary>
/// Shared assertions for raw rule output: metadata consistency of diagnostics,
/// document-relative range validity, and well-formed fix edits.
/// </summary>
internal static class RuleDiagnosticValidator
{
    public static void ValidateDiagnostic(
        RuleMetadata metadata,
        Diagnostic diagnostic,
        IReadOnlyList<string> lines,
        string path,
        List<string> failures)
    {
        var ruleId = metadata.RuleId;

        if (string.IsNullOrWhiteSpace(diagnostic.Message))
        {
            failures.Add($"{ruleId} on '{path}': empty diagnostic message");
        }

        if (diagnostic.Code is not null && !string.Equals(diagnostic.Code, ruleId, StringComparison.Ordinal))
        {
            failures.Add($"{ruleId} on '{path}': Code '{diagnostic.Code}' does not match rule ID");
        }

        if (diagnostic.Data is { } data)
        {
            if (data.RuleId is not null && !string.Equals(data.RuleId, ruleId, StringComparison.Ordinal))
            {
                failures.Add($"{ruleId} on '{path}': Data.RuleId '{data.RuleId}' does not match rule ID");
            }

            if (data.Category is not null && !string.Equals(data.Category, metadata.Category, StringComparison.Ordinal))
            {
                failures.Add($"{ruleId} on '{path}': Data.Category '{data.Category}' does not match metadata category '{metadata.Category}'");
            }

            if (data.Fixable is true && !metadata.Fixable)
            {
                failures.Add($"{ruleId} on '{path}': Data.Fixable is true but the rule metadata is not fixable");
            }
        }

        ValidateRange(ruleId, diagnostic.Range, lines, path, failures, "diagnostic range");
    }

    public static void ValidateFixes(
        IRule rule,
        RuleContext context,
        Diagnostic diagnostic,
        IReadOnlyList<string> lines,
        string path,
        List<string> failures)
    {
        var ruleId = rule.Metadata.RuleId;

        IReadOnlyList<Fix> fixes;
        try
        {
            fixes = rule.GetFixes(context, diagnostic).ToArray();
        }
#pragma warning disable CA1031 // Any rule exception is itself the failure being reported.
        catch (Exception ex)
        {
            failures.Add($"{ruleId} on '{path}': GetFixes threw {ex.GetType().Name}: {ex.Message}");
            return;
        }
#pragma warning restore CA1031

        foreach (var fix in fixes)
        {
            if (string.IsNullOrWhiteSpace(fix.Title))
            {
                failures.Add($"{ruleId} on '{path}': fix has an empty title");
            }

            var orderedEdits = fix.Edits
                .OrderBy(edit => edit.Range.Start.Line)
                .ThenBy(edit => edit.Range.Start.Character)
                .ToArray();

            foreach (var edit in orderedEdits)
            {
                ValidateRange(ruleId, edit.Range, lines, path, failures, "fix edit range");
            }

            for (var i = 1; i < orderedEdits.Length; i++)
            {
                var previousRange = orderedEdits[i - 1].Range;
                var currentRange = orderedEdits[i].Range;
                var previousEnd = previousRange.End;
                var currentStart = currentRange.Start;
                if (currentStart.Line < previousEnd.Line ||
                    (currentStart.Line == previousEnd.Line && currentStart.Character < previousEnd.Character) ||
                    (IsEmpty(previousRange) && IsEmpty(currentRange) && currentStart == previousRange.Start))
                {
                    failures.Add($"{ruleId} on '{path}': fix '{fix.Title}' has overlapping edits");
                    break;
                }
            }
        }
    }

    private static bool IsEmpty(TsqlRefine.PluginSdk.Range range) => range.Start == range.End;

    public static void ValidateRange(
        string ruleId,
        TsqlRefine.PluginSdk.Range range,
        IReadOnlyList<string> lines,
        string path,
        List<string> failures,
        string description)
    {
        var location = $"({range.Start.Line},{range.Start.Character})-({range.End.Line},{range.End.Character})";

        if (range.Start.Line < 0 || range.Start.Character < 0 || range.End.Line < 0 || range.End.Character < 0)
        {
            failures.Add($"{ruleId} on '{path}': negative {description} {location}");
            return;
        }

        if (range.End.Line < range.Start.Line ||
            (range.End.Line == range.Start.Line && range.End.Character < range.Start.Character))
        {
            failures.Add($"{ruleId} on '{path}': end precedes start in {description} {location}");
            return;
        }

        if (range.End.Line >= lines.Count)
        {
            failures.Add($"{ruleId} on '{path}': {description} {location} exceeds the {lines.Count}-line document");
            return;
        }

        if (range.Start.Character > lines[range.Start.Line].Length)
        {
            failures.Add($"{ruleId} on '{path}': {description} start {location} points past the end of line {range.Start.Line} (length {lines[range.Start.Line].Length})");
        }

        if (range.End.Character > lines[range.End.Line].Length)
        {
            failures.Add($"{ruleId} on '{path}': {description} end {location} points past the end of line {range.End.Line} (length {lines[range.End.Line].Length})");
        }
    }
}
