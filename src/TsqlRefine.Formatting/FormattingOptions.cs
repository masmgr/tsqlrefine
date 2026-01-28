namespace TsqlRefine.Formatting;

public enum IndentStyle
{
    Tabs,
    Spaces
}

public enum KeywordCasing
{
    /// <summary>Preserve original casing (no change)</summary>
    Preserve,
    /// <summary>Convert keywords to UPPERCASE</summary>
    Upper,
    /// <summary>Convert keywords to lowercase</summary>
    Lower,
    /// <summary>Convert keywords to PascalCase</summary>
    Pascal
}

public enum IdentifierCasing
{
    /// <summary>Preserve original casing (no change)</summary>
    Preserve,
    /// <summary>Convert identifiers to UPPERCASE</summary>
    Upper,
    /// <summary>Convert identifiers to lowercase</summary>
    Lower,
    /// <summary>Convert identifiers to PascalCase</summary>
    Pascal,
    /// <summary>Convert identifiers to camelCase</summary>
    Camel
}

public enum CommaStyle
{
    /// <summary>Trailing comma: SELECT a, b, c</summary>
    Trailing,
    /// <summary>Leading comma: SELECT a ,b ,c</summary>
    Leading
}

public sealed record FormattingOptions
{
    /// <summary>Indentation style: tabs or spaces</summary>
    public IndentStyle IndentStyle { get; init; } = IndentStyle.Spaces;

    /// <summary>Number of spaces per indent level (for spaces) or tab width (for tabs)</summary>
    public int IndentSize { get; init; } = 4;

    /// <summary>Keyword casing style</summary>
    public KeywordCasing KeywordCasing { get; init; } = KeywordCasing.Upper;

    /// <summary>Identifier casing style</summary>
    public IdentifierCasing IdentifierCasing { get; init; } = IdentifierCasing.Preserve;

    /// <summary>Comma placement style (trailing or leading)</summary>
    public CommaStyle CommaStyle { get; init; } = CommaStyle.Trailing;

    /// <summary>Maximum line length (0 = no limit)</summary>
    public int MaxLineLength { get; init; } = 0;

    /// <summary>Insert final newline at end of file</summary>
    public bool InsertFinalNewline { get; init; } = true;

    /// <summary>Trim trailing whitespace on lines</summary>
    public bool TrimTrailingWhitespace { get; init; } = true;
}

