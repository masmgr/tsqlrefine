using TsqlRefine.PluginSdk;

namespace TsqlRefine.Core.Model;

/// <summary>
/// Represents the analysis results for a single file.
/// </summary>
/// <param name="FilePath">The path to the analyzed file.</param>
/// <param name="Diagnostics">The list of diagnostics (issues) found in the file.</param>
public sealed record FileResult(string FilePath, IReadOnlyList<Diagnostic> Diagnostics);

/// <summary>
/// Represents the complete lint analysis results for one or more files.
/// </summary>
/// <param name="Tool">The name of the tool that produced these results.</param>
/// <param name="Version">The version of the tool.</param>
/// <param name="Command">The command that was executed.</param>
/// <param name="Files">The analysis results for each file.</param>
public sealed record LintResult(
    string Tool,
    string Version,
    string Command,
    IReadOnlyList<FileResult> Files
);

