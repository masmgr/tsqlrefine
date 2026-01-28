using TsqlRefine.Formatting;

namespace TsqlRefine.Core.Config;

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
            KeywordCasing = ParseKeywordCasing(config.KeywordCasing),
            IdentifierCasing = ParseIdentifierCasing(config.IdentifierCasing),
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

    private static KeywordCasing ParseKeywordCasing(string value) => value.ToLowerInvariant() switch
    {
        "preserve" => KeywordCasing.Preserve,
        "upper" => KeywordCasing.Upper,
        "lower" => KeywordCasing.Lower,
        "pascal" => KeywordCasing.Pascal,
        _ => KeywordCasing.Upper
    };

    private static IdentifierCasing ParseIdentifierCasing(string value) => value.ToLowerInvariant() switch
    {
        "preserve" => IdentifierCasing.Preserve,
        "upper" => IdentifierCasing.Upper,
        "lower" => IdentifierCasing.Lower,
        "pascal" => IdentifierCasing.Pascal,
        "camel" => IdentifierCasing.Camel,
        _ => IdentifierCasing.Preserve
    };

    private static CommaStyle ParseCommaStyle(string value) => value.ToLowerInvariant() switch
    {
        "trailing" => CommaStyle.Trailing,
        "leading" => CommaStyle.Leading,
        _ => CommaStyle.Trailing
    };
}
