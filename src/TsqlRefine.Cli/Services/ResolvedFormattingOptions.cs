using TsqlRefine.Formatting;

namespace TsqlRefine.Cli.Services;

/// <summary>
/// Indicates the source from which a formatting option value was resolved.
/// </summary>
public enum FormattingOptionSource
{
    /// <summary>Default value from FormattingOptions</summary>
    Default,
    /// <summary>Value from tsqlrefine.json configuration file</summary>
    Config,
    /// <summary>Value from .editorconfig file</summary>
    EditorConfig,
    /// <summary>Value from CLI argument</summary>
    CliArg
}

/// <summary>
/// A formatting option value with its source.
/// </summary>
public sealed record ResolvedFormattingOption<T>(
    T Value,
    FormattingOptionSource Source
);

/// <summary>
/// All formatting options resolved with their sources.
/// </summary>
public sealed record ResolvedFormattingOptions(
    ResolvedFormattingOption<int> CompatLevel,
    ResolvedFormattingOption<IndentStyle> IndentStyle,
    ResolvedFormattingOption<int> IndentSize,
    ResolvedFormattingOption<ElementCasing> KeywordCasing,
    ResolvedFormattingOption<ElementCasing> BuiltInFunctionCasing,
    ResolvedFormattingOption<ElementCasing> DataTypeCasing,
    ResolvedFormattingOption<ElementCasing> SchemaCasing,
    ResolvedFormattingOption<ElementCasing> TableCasing,
    ResolvedFormattingOption<ElementCasing> ColumnCasing,
    ResolvedFormattingOption<ElementCasing> VariableCasing,
    ResolvedFormattingOption<CommaStyle> CommaStyle,
    ResolvedFormattingOption<int> MaxLineLength,
    ResolvedFormattingOption<bool> InsertFinalNewline,
    ResolvedFormattingOption<bool> TrimTrailingWhitespace,
    ResolvedFormattingOption<bool> NormalizeInlineSpacing,
    ResolvedFormattingOption<bool> NormalizeOperatorSpacing,
    ResolvedFormattingOption<bool> NormalizeKeywordSpacing,
    ResolvedFormattingOption<LineEnding> LineEnding
)
{
    /// <summary>
    /// Path to the tsqlrefine.json config file if used, null otherwise.
    /// </summary>
    public string? ConfigPath { get; init; }

    /// <summary>
    /// Path to the .editorconfig file if used, null otherwise.
    /// </summary>
    public string? EditorConfigPath { get; init; }

    /// <summary>
    /// Path for which the options were resolved.
    /// </summary>
    public string? ResolvedForPath { get; init; }
}
