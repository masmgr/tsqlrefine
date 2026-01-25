using TsqlRefine.PluginSdk;

namespace TsqlRefine.Core.Model;

public sealed record AppliedFix(
    string RuleId,
    string Title,
    IReadOnlyList<TextEdit> Edits
);

public sealed record SkippedFix(
    string RuleId,
    string Title,
    string Reason
);

public sealed record FixedFileResult(
    string FilePath,
    string OriginalText,
    string FixedText,
    IReadOnlyList<Diagnostic> Diagnostics,
    IReadOnlyList<AppliedFix> AppliedFixes,
    IReadOnlyList<SkippedFix> SkippedFixes
);

public sealed record FixResult(
    string Tool,
    string Version,
    string Command,
    IReadOnlyList<FixedFileResult> Files
);
