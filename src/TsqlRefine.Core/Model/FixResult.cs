using TsqlRefine.PluginSdk;

namespace TsqlRefine.Core.Model;

/// <summary>
/// Represents an auto-fix that was successfully applied to resolve a diagnostic.
/// </summary>
/// <param name="RuleId">The identifier of the rule that provided this fix.</param>
/// <param name="Title">A human-readable description of what this fix does.</param>
/// <param name="Edits">The text edits that were applied.</param>
public sealed record AppliedFix(
    string RuleId,
    string Title,
    IReadOnlyList<TextEdit> Edits
);

/// <summary>
/// Represents an auto-fix that could not be applied.
/// </summary>
/// <param name="RuleId">The identifier of the rule that provided this fix.</param>
/// <param name="Title">A human-readable description of what this fix would have done.</param>
/// <param name="Reason">The reason why the fix was not applied.</param>
public sealed record SkippedFix(
    string RuleId,
    string Title,
    string Reason
);

/// <summary>
/// Represents the fix results for a single file.
/// </summary>
/// <param name="FilePath">The path to the fixed file.</param>
/// <param name="OriginalText">The original file content before fixes.</param>
/// <param name="FixedText">The file content after applying fixes.</param>
/// <param name="Diagnostics">All diagnostics found in the file.</param>
/// <param name="AppliedFixes">The fixes that were successfully applied.</param>
/// <param name="SkippedFixes">The fixes that were not applied.</param>
public sealed record FixedFileResult(
    string FilePath,
    string OriginalText,
    string FixedText,
    IReadOnlyList<Diagnostic> Diagnostics,
    IReadOnlyList<AppliedFix> AppliedFixes,
    IReadOnlyList<SkippedFix> SkippedFixes
);

/// <summary>
/// Represents the complete fix results for one or more files.
/// </summary>
/// <param name="Tool">The name of the tool that produced these results.</param>
/// <param name="Version">The version of the tool.</param>
/// <param name="Command">The command that was executed.</param>
/// <param name="Files">The fix results for each file.</param>
public sealed record FixResult(
    string Tool,
    string Version,
    string Command,
    IReadOnlyList<FixedFileResult> Files
);
