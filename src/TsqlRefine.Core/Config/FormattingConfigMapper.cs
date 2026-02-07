using TsqlRefine.Formatting;

namespace TsqlRefine.Core.Config;

/// <summary>
/// Maps between configuration file formatting settings and internal formatting options.
/// </summary>
public static class FormattingConfigMapper
{
    public static FormattingOptions ToFormattingOptions(FormattingConfig? config)
    {
        if (config is null)
        {
            return new FormattingOptions();
        }

        return new FormattingOptions
        {
            IndentStyle = ParseIndentStyle(config.IndentStyle),
            IndentSize = config.IndentSize,
            KeywordElementCasing = ParseElementCasing(config.KeywordCasing),
            BuiltInFunctionCasing = ParseElementCasing(config.FunctionCasing),
            DataTypeCasing = ParseElementCasing(config.DataTypeCasing),
            SchemaCasing = ParseElementCasing(config.SchemaCasing),
            TableCasing = ParseElementCasing(config.TableCasing),
            ColumnCasing = ParseElementCasing(config.ColumnCasing),
            VariableCasing = ParseElementCasing(config.VariableCasing),
            CommaStyle = ParseCommaStyle(config.CommaStyle),
            MaxLineLength = config.MaxLineLength,
            InsertFinalNewline = config.InsertFinalNewline,
            TrimTrailingWhitespace = config.TrimTrailingWhitespace
        };
    }

    private static IndentStyle ParseIndentStyle(string value) => value.ToLowerInvariant() switch
    {
        "tabs" => IndentStyle.Tabs,
        "spaces" => IndentStyle.Spaces,
        _ => IndentStyle.Spaces
    };

    private static ElementCasing ParseElementCasing(string value) => value.ToLowerInvariant() switch
    {
        "none" => ElementCasing.None,
        "upper" => ElementCasing.Upper,
        "lower" => ElementCasing.Lower,
        _ => ElementCasing.None
    };

    private static CommaStyle ParseCommaStyle(string value) => value.ToLowerInvariant() switch
    {
        "trailing" => CommaStyle.Trailing,
        "leading" => CommaStyle.Leading,
        _ => CommaStyle.Trailing
    };
}
