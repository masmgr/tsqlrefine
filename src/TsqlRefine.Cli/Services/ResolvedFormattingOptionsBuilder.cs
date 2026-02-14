using TsqlRefine.Formatting;

namespace TsqlRefine.Cli.Services;

/// <summary>
/// Builder for constructing ResolvedFormattingOptions with proper layering of sources.
/// </summary>
internal sealed class ResolvedFormattingOptionsBuilder
{
    private readonly FormattingOptions _defaults = new();

    private ResolvedFormattingOption<int> _compatLevel;
    private ResolvedFormattingOption<IndentStyle> _indentStyle;
    private ResolvedFormattingOption<int> _indentSize;
    private ResolvedFormattingOption<ElementCasing> _keywordCasing;
    private ResolvedFormattingOption<ElementCasing> _builtInFunctionCasing;
    private ResolvedFormattingOption<ElementCasing> _dataTypeCasing;
    private ResolvedFormattingOption<ElementCasing> _schemaCasing;
    private ResolvedFormattingOption<ElementCasing> _tableCasing;
    private ResolvedFormattingOption<ElementCasing> _columnCasing;
    private ResolvedFormattingOption<ElementCasing> _variableCasing;
    private ResolvedFormattingOption<ElementCasing> _systemTableCasing;
    private ResolvedFormattingOption<ElementCasing> _storedProcedureCasing;
    private ResolvedFormattingOption<ElementCasing> _userDefinedFunctionCasing;
    private ResolvedFormattingOption<CommaStyle> _commaStyle;
    private ResolvedFormattingOption<int> _maxLineLength;
    private ResolvedFormattingOption<bool> _insertFinalNewline;
    private ResolvedFormattingOption<bool> _trimTrailingWhitespace;
    private ResolvedFormattingOption<bool> _normalizeInlineSpacing;
    private ResolvedFormattingOption<bool> _normalizeOperatorSpacing;
    private ResolvedFormattingOption<bool> _normalizeKeywordSpacing;
    private ResolvedFormattingOption<bool> _normalizeFunctionSpacing;
    private ResolvedFormattingOption<LineEnding> _lineEnding;
    private ResolvedFormattingOption<int> _maxConsecutiveBlankLines;
    private ResolvedFormattingOption<bool> _trimLeadingBlankLines;

    public string? ConfigPath { get; set; }
    public string? EditorConfigPath { get; set; }
    public string? ResolvedForPath { get; set; }

    public ResolvedFormattingOptionsBuilder()
    {
        // Initialize with defaults
        _compatLevel = new(_defaults.CompatLevel, FormattingOptionSource.Default);
        _indentStyle = new(_defaults.IndentStyle, FormattingOptionSource.Default);
        _indentSize = new(_defaults.IndentSize, FormattingOptionSource.Default);
        _keywordCasing = new(_defaults.KeywordElementCasing, FormattingOptionSource.Default);
        _builtInFunctionCasing = new(_defaults.BuiltInFunctionCasing, FormattingOptionSource.Default);
        _dataTypeCasing = new(_defaults.DataTypeCasing, FormattingOptionSource.Default);
        _schemaCasing = new(_defaults.SchemaCasing, FormattingOptionSource.Default);
        _tableCasing = new(_defaults.TableCasing, FormattingOptionSource.Default);
        _columnCasing = new(_defaults.ColumnCasing, FormattingOptionSource.Default);
        _variableCasing = new(_defaults.VariableCasing, FormattingOptionSource.Default);
        _systemTableCasing = new(_defaults.SystemTableCasing, FormattingOptionSource.Default);
        _storedProcedureCasing = new(_defaults.StoredProcedureCasing, FormattingOptionSource.Default);
        _userDefinedFunctionCasing = new(_defaults.UserDefinedFunctionCasing, FormattingOptionSource.Default);
        _commaStyle = new(_defaults.CommaStyle, FormattingOptionSource.Default);
        _maxLineLength = new(_defaults.MaxLineLength, FormattingOptionSource.Default);
        _insertFinalNewline = new(_defaults.InsertFinalNewline, FormattingOptionSource.Default);
        _trimTrailingWhitespace = new(_defaults.TrimTrailingWhitespace, FormattingOptionSource.Default);
        _normalizeInlineSpacing = new(_defaults.NormalizeInlineSpacing, FormattingOptionSource.Default);
        _normalizeOperatorSpacing = new(_defaults.NormalizeOperatorSpacing, FormattingOptionSource.Default);
        _normalizeKeywordSpacing = new(_defaults.NormalizeKeywordSpacing, FormattingOptionSource.Default);
        _normalizeFunctionSpacing = new(_defaults.NormalizeFunctionSpacing, FormattingOptionSource.Default);
        _lineEnding = new(_defaults.LineEnding, FormattingOptionSource.Default);
        _maxConsecutiveBlankLines = new(_defaults.MaxConsecutiveBlankLines, FormattingOptionSource.Default);
        _trimLeadingBlankLines = new(_defaults.TrimLeadingBlankLines, FormattingOptionSource.Default);
    }

    /// <summary>
    /// Applies values from tsqlrefine.json config, only overriding values that differ from defaults.
    /// </summary>
    public ResolvedFormattingOptionsBuilder ApplyConfig(FormattingOptions configOptions)
    {
        if (configOptions.CompatLevel != _defaults.CompatLevel)
            _compatLevel = new(configOptions.CompatLevel, FormattingOptionSource.Config);
        if (configOptions.IndentStyle != _defaults.IndentStyle)
            _indentStyle = new(configOptions.IndentStyle, FormattingOptionSource.Config);
        if (configOptions.IndentSize != _defaults.IndentSize)
            _indentSize = new(configOptions.IndentSize, FormattingOptionSource.Config);
        if (configOptions.KeywordElementCasing != _defaults.KeywordElementCasing)
            _keywordCasing = new(configOptions.KeywordElementCasing, FormattingOptionSource.Config);
        if (configOptions.BuiltInFunctionCasing != _defaults.BuiltInFunctionCasing)
            _builtInFunctionCasing = new(configOptions.BuiltInFunctionCasing, FormattingOptionSource.Config);
        if (configOptions.DataTypeCasing != _defaults.DataTypeCasing)
            _dataTypeCasing = new(configOptions.DataTypeCasing, FormattingOptionSource.Config);
        if (configOptions.SchemaCasing != _defaults.SchemaCasing)
            _schemaCasing = new(configOptions.SchemaCasing, FormattingOptionSource.Config);
        if (configOptions.TableCasing != _defaults.TableCasing)
            _tableCasing = new(configOptions.TableCasing, FormattingOptionSource.Config);
        if (configOptions.ColumnCasing != _defaults.ColumnCasing)
            _columnCasing = new(configOptions.ColumnCasing, FormattingOptionSource.Config);
        if (configOptions.VariableCasing != _defaults.VariableCasing)
            _variableCasing = new(configOptions.VariableCasing, FormattingOptionSource.Config);
        if (configOptions.SystemTableCasing != _defaults.SystemTableCasing)
            _systemTableCasing = new(configOptions.SystemTableCasing, FormattingOptionSource.Config);
        if (configOptions.StoredProcedureCasing != _defaults.StoredProcedureCasing)
            _storedProcedureCasing = new(configOptions.StoredProcedureCasing, FormattingOptionSource.Config);
        if (configOptions.UserDefinedFunctionCasing != _defaults.UserDefinedFunctionCasing)
            _userDefinedFunctionCasing = new(configOptions.UserDefinedFunctionCasing, FormattingOptionSource.Config);
        if (configOptions.CommaStyle != _defaults.CommaStyle)
            _commaStyle = new(configOptions.CommaStyle, FormattingOptionSource.Config);
        if (configOptions.MaxLineLength != _defaults.MaxLineLength)
            _maxLineLength = new(configOptions.MaxLineLength, FormattingOptionSource.Config);
        if (configOptions.InsertFinalNewline != _defaults.InsertFinalNewline)
            _insertFinalNewline = new(configOptions.InsertFinalNewline, FormattingOptionSource.Config);
        if (configOptions.TrimTrailingWhitespace != _defaults.TrimTrailingWhitespace)
            _trimTrailingWhitespace = new(configOptions.TrimTrailingWhitespace, FormattingOptionSource.Config);
        if (configOptions.NormalizeInlineSpacing != _defaults.NormalizeInlineSpacing)
            _normalizeInlineSpacing = new(configOptions.NormalizeInlineSpacing, FormattingOptionSource.Config);
        if (configOptions.NormalizeOperatorSpacing != _defaults.NormalizeOperatorSpacing)
            _normalizeOperatorSpacing = new(configOptions.NormalizeOperatorSpacing, FormattingOptionSource.Config);
        if (configOptions.NormalizeKeywordSpacing != _defaults.NormalizeKeywordSpacing)
            _normalizeKeywordSpacing = new(configOptions.NormalizeKeywordSpacing, FormattingOptionSource.Config);
        if (configOptions.NormalizeFunctionSpacing != _defaults.NormalizeFunctionSpacing)
            _normalizeFunctionSpacing = new(configOptions.NormalizeFunctionSpacing, FormattingOptionSource.Config);
        if (configOptions.LineEnding != _defaults.LineEnding)
            _lineEnding = new(configOptions.LineEnding, FormattingOptionSource.Config);
        if (configOptions.MaxConsecutiveBlankLines != _defaults.MaxConsecutiveBlankLines)
            _maxConsecutiveBlankLines = new(configOptions.MaxConsecutiveBlankLines, FormattingOptionSource.Config);
        if (configOptions.TrimLeadingBlankLines != _defaults.TrimLeadingBlankLines)
            _trimLeadingBlankLines = new(configOptions.TrimLeadingBlankLines, FormattingOptionSource.Config);

        return this;
    }

    /// <summary>
    /// Applies values from .editorconfig (only indentStyle, indentSize, lineEnding are supported).
    /// </summary>
    public ResolvedFormattingOptionsBuilder ApplyEditorConfig(
        IndentStyle? indentStyle,
        int? indentSize,
        LineEnding? lineEnding)
    {
        if (indentStyle.HasValue)
            _indentStyle = new(indentStyle.Value, FormattingOptionSource.EditorConfig);
        if (indentSize.HasValue)
            _indentSize = new(indentSize.Value, FormattingOptionSource.EditorConfig);
        if (lineEnding.HasValue)
            _lineEnding = new(lineEnding.Value, FormattingOptionSource.EditorConfig);

        return this;
    }

    /// <summary>
    /// Applies values from CLI arguments (highest priority).
    /// </summary>
    public ResolvedFormattingOptionsBuilder ApplyCliArgs(
        IndentStyle? indentStyle,
        int? indentSize,
        LineEnding? lineEnding)
    {
        if (indentStyle.HasValue)
            _indentStyle = new(indentStyle.Value, FormattingOptionSource.CliArg);
        if (indentSize.HasValue)
            _indentSize = new(indentSize.Value, FormattingOptionSource.CliArg);
        if (lineEnding.HasValue)
            _lineEnding = new(lineEnding.Value, FormattingOptionSource.CliArg);

        return this;
    }

    public ResolvedFormattingOptions Build()
    {
        return new ResolvedFormattingOptions(
            CompatLevel: _compatLevel,
            IndentStyle: _indentStyle,
            IndentSize: _indentSize,
            KeywordCasing: _keywordCasing,
            BuiltInFunctionCasing: _builtInFunctionCasing,
            DataTypeCasing: _dataTypeCasing,
            SchemaCasing: _schemaCasing,
            TableCasing: _tableCasing,
            ColumnCasing: _columnCasing,
            VariableCasing: _variableCasing,
            SystemTableCasing: _systemTableCasing,
            StoredProcedureCasing: _storedProcedureCasing,
            UserDefinedFunctionCasing: _userDefinedFunctionCasing,
            CommaStyle: _commaStyle,
            MaxLineLength: _maxLineLength,
            InsertFinalNewline: _insertFinalNewline,
            TrimTrailingWhitespace: _trimTrailingWhitespace,
            NormalizeInlineSpacing: _normalizeInlineSpacing,
            NormalizeOperatorSpacing: _normalizeOperatorSpacing,
            NormalizeKeywordSpacing: _normalizeKeywordSpacing,
            NormalizeFunctionSpacing: _normalizeFunctionSpacing,
            LineEnding: _lineEnding,
            MaxConsecutiveBlankLines: _maxConsecutiveBlankLines,
            TrimLeadingBlankLines: _trimLeadingBlankLines
        )
        {
            ConfigPath = ConfigPath,
            EditorConfigPath = EditorConfigPath,
            ResolvedForPath = ResolvedForPath
        };
    }
}
