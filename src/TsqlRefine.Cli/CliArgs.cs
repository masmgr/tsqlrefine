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
    string Output,
    DiagnosticSeverity? MinimumSeverity,
    string? Preset,
    int? CompatLevel,
    string? RulesetPath,
    IndentStyle? IndentStyle,
    int? IndentSize,
    bool Verbose,
    bool ShowSources,
    IReadOnlyList<string> Paths,
    string? RuleId
);
