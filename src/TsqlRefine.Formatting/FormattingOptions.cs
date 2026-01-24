namespace TsqlRefine.Formatting;

public enum IndentStyle
{
    Tabs,
    Spaces
}

public sealed record FormattingOptions(
    IndentStyle IndentStyle = IndentStyle.Spaces,
    int IndentSize = 4
);

