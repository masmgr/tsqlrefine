namespace TsqlRefine.Formatting;

public enum IndentStyle
{
    Tabs,
    Spaces
}

public enum ElementCasing
{
    /// <summary>No casing change (preserve original)</summary>
    None,
    /// <summary>Convert to UPPERCASE</summary>
    Upper,
    /// <summary>Convert to lowercase</summary>
    Lower
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

    /// <summary>Casing for SQL keywords (SELECT, FROM, WHERE, etc.)</summary>
    public ElementCasing KeywordElementCasing { get; init; } = ElementCasing.Upper;

    /// <summary>Casing for built-in functions (COUNT, SUM, GETDATE, etc.)</summary>
    public ElementCasing BuiltInFunctionCasing { get; init; } = ElementCasing.Upper;

    /// <summary>Casing for data types (INT, VARCHAR, DATETIME, etc.)</summary>
    public ElementCasing DataTypeCasing { get; init; } = ElementCasing.Lower;

    /// <summary>Casing for schema names (dbo, sys, etc.)</summary>
    public ElementCasing SchemaCasing { get; init; } = ElementCasing.Lower;

    /// <summary>Casing for table names and aliases</summary>
    public ElementCasing TableCasing { get; init; } = ElementCasing.Upper;

    /// <summary>Casing for column names and aliases</summary>
    public ElementCasing ColumnCasing { get; init; } = ElementCasing.Upper;

    /// <summary>Casing for variables (@var, @@rowcount, etc.)</summary>
    public ElementCasing VariableCasing { get; init; } = ElementCasing.Lower;

    /// <summary>Comma placement style (trailing or leading)</summary>
    public CommaStyle CommaStyle { get; init; } = CommaStyle.Trailing;

    /// <summary>Maximum line length (0 = no limit)</summary>
    public int MaxLineLength { get; init; } = 0;

    /// <summary>Insert final newline at end of file</summary>
    public bool InsertFinalNewline { get; init; } = true;

    /// <summary>Trim trailing whitespace on lines</summary>
    public bool TrimTrailingWhitespace { get; init; } = true;

    /// <summary>Normalize inline spacing (collapse duplicates, space after commas)</summary>
    public bool NormalizeInlineSpacing { get; init; } = true;
}

