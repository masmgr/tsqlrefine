using TsqlRefine.Formatting;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Cli;

/// <summary>
/// Parsed command-line arguments for the TsqlRefine CLI application.
/// </summary>
/// <param name="Command">The command to execute (lint, format, fix, etc.).</param>
/// <param name="IsExplicitCommand">Whether the command was explicitly provided or inferred.</param>
/// <param name="ConfigPath">Path to tsqlrefine.json configuration file.</param>
/// <param name="IgnoreListPath">Path to ignore list file.</param>
/// <param name="DetectEncoding">Whether to detect file encoding automatically.</param>
/// <param name="Stdin">Whether to read from standard input.</param>
/// <param name="Output">Output format (text, json, etc.).</param>
/// <param name="MinimumSeverity">Minimum diagnostic severity to report.</param>
/// <param name="Preset">Preset ruleset name to use.</param>
/// <param name="CompatLevel">SQL Server compatibility level override.</param>
/// <param name="RulesetPath">Path to custom ruleset file.</param>
/// <param name="IndentStyle">Indentation style override (spaces or tabs).</param>
/// <param name="IndentSize">Indentation size override.</param>
/// <param name="LineEnding">Line ending style override.</param>
/// <param name="Verbose">Whether to enable verbose output.</param>
/// <param name="ShowSources">Whether to show formatting option sources.</param>
/// <param name="Paths">File paths to analyze.</param>
/// <param name="RuleId">Specific rule ID to run (for single-rule mode).</param>
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
    LineEnding? LineEnding,
    bool Verbose,
    bool ShowSources,
    IReadOnlyList<string> Paths,
    string? RuleId
);
