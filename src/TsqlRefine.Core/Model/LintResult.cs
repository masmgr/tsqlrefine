using TsqlRefine.PluginSdk;

namespace TsqlRefine.Core.Model;

public sealed record FileResult(string FilePath, IReadOnlyList<Diagnostic> Diagnostics);

public sealed record LintResult(
    string Tool,
    string Version,
    string Command,
    IReadOnlyList<FileResult> Files
);

