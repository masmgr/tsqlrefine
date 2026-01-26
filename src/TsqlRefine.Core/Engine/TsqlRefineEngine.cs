using System.Reflection;
using System.Text;
using TsqlRefine.Core.Config;
using TsqlRefine.Core.Model;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Core.Engine;

public sealed class TsqlRefineEngine
{
    private readonly IReadOnlyList<IRule> _rules;

    public TsqlRefineEngine(IEnumerable<IRule> rules)
    {
        _rules = rules?.ToArray() ?? Array.Empty<IRule>();
    }

    public LintResult Run(string command, IEnumerable<SqlInput> inputs, EngineOptions options)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(options);

        var activeRules = GetActiveRules(options);
        var inputList = inputs as IList<SqlInput> ?? inputs.ToList();
        var files = new FileResult[inputList.Count];
        for (var i = 0; i < inputList.Count; i++)
        {
            files[i] = AnalyzeFile(inputList[i], activeRules, options);
        }
        return new LintResult(
            Tool: "tsqlrefine",
            Version: GetVersion(),
            Command: command,
            Files: files
        );
    }

    public FixResult Fix(IEnumerable<SqlInput> inputs, EngineOptions options)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(options);

        var activeRules = GetActiveRules(options);
        var inputList = inputs as IList<SqlInput> ?? inputs.ToList();
        var files = new FixedFileResult[inputList.Count];
        for (var i = 0; i < inputList.Count; i++)
        {
            files[i] = FixFile(inputList[i], activeRules, options);
        }
        return new FixResult(
            Tool: "tsqlrefine",
            Version: GetVersion(),
            Command: "fix",
            Files: files
        );
    }

    private static FileResult AnalyzeFile(SqlInput input, IReadOnlyList<IRule> rules, EngineOptions options)
    {
        var diagnostics = new List<Diagnostic>();
        var context = CreateContext(input, options);

        foreach (var rule in rules)
        {
            AppendDiagnostics(rule, context, options, diagnostics, fixGroups: null);
        }

        return new FileResult(input.FilePath, diagnostics);
    }

    private static FixedFileResult FixFile(SqlInput input, IReadOnlyList<IRule> rules, EngineOptions options)
    {
        var context = CreateContext(input, options);

        var diagnostics = new List<Diagnostic>();
        var fixGroups = new List<DiagnosticFixGroup>();

        foreach (var rule in rules)
        {
            AppendDiagnostics(rule, context, options, diagnostics, fixGroups);
        }

        var fixOutcome = ApplyFixes(input.Text, fixGroups);
        var fixedInput = new SqlInput(input.FilePath, fixOutcome.FixedText);
        var finalDiagnostics = AnalyzeFile(fixedInput, rules, options).Diagnostics;

        return new FixedFileResult(
            FilePath: input.FilePath,
            OriginalText: input.Text,
            FixedText: fixOutcome.FixedText,
            Diagnostics: finalDiagnostics,
            AppliedFixes: fixOutcome.AppliedFixes,
            SkippedFixes: fixOutcome.SkippedFixes
        );
    }

    private static FixOutcome ApplyFixes(string text, IReadOnlyList<DiagnosticFixGroup> fixGroups)
    {
        if (fixGroups.Count == 0)
        {
            return new FixOutcome(text, Array.Empty<AppliedFix>(), Array.Empty<SkippedFix>());
        }

        var lineMap = BuildLineMap(text);
        var orderedGroups = fixGroups
            .OrderBy(g => g.Diagnostic.Range.Start.Line)
            .ThenBy(g => g.Diagnostic.Range.Start.Character)
            .ThenBy(g => g.Diagnostic.Range.End.Line)
            .ThenBy(g => g.Diagnostic.Range.End.Character)
            .ThenBy(g => g.Rule.Metadata.RuleId, StringComparer.Ordinal)
            .ToArray();

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

    private static bool TryResolveFix(Fix fix, LineMap lineMap, out ResolvedFix resolvedFix, out string reason)
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
            if (!TryResolveRange(edit.Range, lineMap, out var start, out var end))
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

    private static bool TryResolveRange(TsqlRefine.PluginSdk.Range range, LineMap lineMap, out int start, out int end)
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

    private static bool TryGetOffset(LineMap lineMap, Position position, out int offset)
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

    private static LineMap BuildLineMap(string text)
    {
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

    private static Diagnostic NormalizeDiagnostic(IRule rule, Diagnostic diagnostic)
    {
        var data = diagnostic.Data ?? new DiagnosticData();
        if (data.RuleId is null || data.Category is null || data.Fixable is null)
        {
            data = data with
            {
                RuleId = data.RuleId ?? rule.Metadata.RuleId,
                Category = data.Category ?? rule.Metadata.Category,
                Fixable = data.Fixable ?? rule.Metadata.Fixable
            };
        }

        var severity = diagnostic.Severity ?? Map(rule.Metadata.DefaultSeverity);
        return diagnostic with
        {
            Severity = severity,
            Code = diagnostic.Code ?? rule.Metadata.RuleId,
            Data = data
        };
    }

    private static DiagnosticSeverity Map(RuleSeverity severity) =>
        severity switch
        {
            RuleSeverity.Error => DiagnosticSeverity.Error,
            RuleSeverity.Warning => DiagnosticSeverity.Warning,
            RuleSeverity.Information => DiagnosticSeverity.Information,
            RuleSeverity.Hint => DiagnosticSeverity.Hint,
            _ => DiagnosticSeverity.Warning
        };

    private static bool IsAtOrAbove(DiagnosticSeverity value, DiagnosticSeverity threshold) =>
        value <= threshold;

    private static string GetVersion()
    {
        var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        return asm.GetName().Version?.ToString() ?? "0.0.0";
    }

    private IReadOnlyList<IRule> GetActiveRules(EngineOptions options)
    {
        var ruleset = options.Ruleset;
        return ruleset is null
            ? _rules
            : _rules.Where(r => ruleset.IsRuleEnabled(r.Metadata.RuleId)).ToArray();
    }

    private static readonly TsqlRefine.PluginSdk.Range ZeroRange =
        new(new Position(0, 0), new Position(0, 0));

    private static RuleContext CreateContext(SqlInput input, EngineOptions options)
    {
        var ruleSettings = options.RuleSettings ?? new RuleSettings();
        var analysis = ScriptDomTokenizer.Analyze(input.Text, options.CompatLevel);
        return new RuleContext(
            FilePath: input.FilePath,
            CompatLevel: options.CompatLevel,
            Ast: analysis.Ast,
            Tokens: analysis.Tokens,
            Settings: ruleSettings
        );
    }

    private static void AppendDiagnostics(
        IRule rule,
        RuleContext context,
        EngineOptions options,
        List<Diagnostic> diagnostics,
        List<DiagnosticFixGroup>? fixGroups)
    {
#pragma warning disable CA1031 // We intentionally isolate rule failures into diagnostics.
        try
        {
            foreach (var diagnostic in rule.Analyze(context) ?? Array.Empty<Diagnostic>())
            {
                var normalized = NormalizeDiagnostic(rule, diagnostic);
                if (!IsAtOrAbove(normalized.Severity ?? DiagnosticSeverity.Warning, options.MinimumSeverity))
                {
                    continue;
                }

                diagnostics.Add(normalized);

                if (fixGroups is null)
                {
                    continue;
                }

                if (normalized.Data?.Fixable is true && rule.Metadata.Fixable)
                {
                    try
                    {
                        var fixes = (rule.GetFixes(context, normalized) ?? Array.Empty<Fix>())
                            .Where(f => f.Edits is not null && f.Edits.Count > 0)
                            .ToArray();
                        if (fixes.Length > 0)
                        {
                            fixGroups.Add(new DiagnosticFixGroup(rule, normalized, fixes));
                        }
                    }
                    catch (Exception ex)
                    {
                        diagnostics.Add(CreateRuleExceptionDiagnostic(rule, ex, isFix: true));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            diagnostics.Add(CreateRuleExceptionDiagnostic(rule, ex, isFix: false));
        }
#pragma warning restore CA1031
    }

    private static Diagnostic CreateRuleExceptionDiagnostic(IRule rule, Exception ex, bool isFix)
    {
        var suffix = isFix ? "fix crashed" : "crashed";
        return new Diagnostic(
            Range: ZeroRange,
            Message: $"Rule '{rule.Metadata.RuleId}' {suffix}: {ex.GetType().Name}: {ex.Message}",
            Severity: DiagnosticSeverity.Error,
            Code: rule.Metadata.RuleId,
            Data: new DiagnosticData(rule.Metadata.RuleId, rule.Metadata.Category, rule.Metadata.Fixable)
        );
    }

    private sealed record DiagnosticFixGroup(IRule Rule, Diagnostic Diagnostic, IReadOnlyList<Fix> Fixes);

    private sealed record FixOutcome(
        string FixedText,
        IReadOnlyList<AppliedFix> AppliedFixes,
        IReadOnlyList<SkippedFix> SkippedFixes
    );

    private sealed record ResolvedFix(IReadOnlyList<ResolvedEdit> Edits);

    private sealed record ResolvedEdit(int Start, int End, string NewText);

    private sealed record TextRange(int Start, int End);

    private sealed record LineMap(IReadOnlyList<int> LineStarts, IReadOnlyList<int> LineLengths);
}
