using TsqlRefine.Formatting;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Cli;

public sealed record CliArgs(
    string Command,
    bool IsExplicitCommand,
    string? ConfigPath,
    string? IgnoreListPath,
    bool DetectEncoding,
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
    bool Verbose,
    IReadOnlyList<string> Paths
);
