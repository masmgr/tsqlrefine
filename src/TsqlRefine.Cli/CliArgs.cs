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
/// <param name="Utf8">Whether to set console encoding to UTF-8.</param>
/// <param name="Output">Output format (text, json, etc.).</param>
/// <param name="MinimumSeverity">Minimum diagnostic severity to report.</param>
/// <param name="Preset">Preset ruleset name to use.</param>
/// <param name="CompatLevel">SQL Server compatibility level override.</param>
/// <param name="RulesetPath">Path to custom ruleset file.</param>
/// <param name="IndentStyle">Indentation style override (spaces or tabs).</param>
/// <param name="IndentSize">Indentation size override.</param>
/// <param name="LineEnding">Line ending style override.</param>
/// <param name="Verbose">Whether to enable verbose output.</param>
/// <param name="Quiet">Whether to suppress informational stderr output (for IDE/extension integration).</param>
/// <param name="ShowSources">Whether to show formatting option sources.</param>
/// <param name="Force">Whether to overwrite existing files (for init command).</param>
/// <param name="Global">Whether to create configuration in home directory (for init command).</param>
/// <param name="Category">Filter rules by category (for list-rules command).</param>
/// <param name="FixableOnly">Show only fixable rules (for list-rules command).</param>
/// <param name="Paths">File paths to analyze.</param>
/// <param name="RuleId">Specific rule ID to run (for single-rule mode).</param>
/// <param name="MaxFileSize">Maximum file size in bytes (0 = unlimited).</param>
/// <param name="AllowPlugins">Whether to allow loading plugin DLLs from configuration.</param>
public sealed record CliArgs(
    string Command,
    bool IsExplicitCommand,
    string? ConfigPath,
    string? IgnoreListPath,
    bool DetectEncoding,
    bool Stdin,
    bool Utf8,
    string Output,
    DiagnosticSeverity? MinimumSeverity,
    string? Preset,
    int? CompatLevel,
    string? RulesetPath,
    IndentStyle? IndentStyle,
    int? IndentSize,
    LineEnding? LineEnding,
    bool Verbose,
    bool Quiet,
    bool ShowSources,
    bool Force,
    bool Global,
    string? Category,
    bool FixableOnly,
    IReadOnlyList<string> Paths,
    string? RuleId,
    long MaxFileSize,
    bool AllowPlugins
);
