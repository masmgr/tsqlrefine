using TsqlRefine.Formatting;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Cli;

public sealed record CliArgs(
    string Command,
    bool ShowHelp,
    bool ShowVersion,
    string? ConfigPath,
    string? IgnoreListPath,
    bool Stdin,
    string? StdinFilePath,
    string Output,
    DiagnosticSeverity? MinimumSeverity,
    string? Preset,
    int? CompatLevel,
    string? RulesetPath,
    bool Write,
    bool Diff,
    IndentStyle? IndentStyle,
    int? IndentSize,
    IReadOnlyList<string> Paths
);

