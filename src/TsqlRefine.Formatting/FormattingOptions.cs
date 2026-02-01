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
    /// <summary>
    /// Leading comma style:
    /// SELECT a
    ///      , b
    ///      , c
    /// </summary>
    Leading
}

public enum LineEnding
{
    /// <summary>Auto-detect from input, fallback to CRLF (Windows-preferred)</summary>
    Auto,
    /// <summary>Unix style (LF)</summary>
    Lf,
    /// <summary>Windows style (CRLF)</summary>
    CrLf
}

public sealed record FormattingOptions
{
    /// <summary>SQL Server compatibility level (100=2008, 110=2012, 120=2014, 150=2019, 160=2022). Default: 150</summary>
    public int CompatLevel { get; init; } = 150;

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

    /// <summary>
    /// Casing for schema names (dbo, sys, etc.).
    /// WARNING: Case-Sensitive collation environments may break queries if casing is changed.
    /// Default is None (preserve original) for safety.
    /// </summary>
    public ElementCasing SchemaCasing { get; init; } = ElementCasing.None;

    /// <summary>
    /// Casing for table names and aliases.
    /// WARNING: Case-Sensitive collation environments may break queries if casing is changed.
    /// Default is None (preserve original) for safety.
    /// </summary>
    public ElementCasing TableCasing { get; init; } = ElementCasing.None;

    /// <summary>
    /// Casing for column names and aliases.
    /// WARNING: Case-Sensitive collation environments may break queries if casing is changed.
    /// Default is None (preserve original) for safety.
    /// </summary>
    public ElementCasing ColumnCasing { get; init; } = ElementCasing.None;

    /// <summary>Casing for variables (@var, @@rowcount, etc.)</summary>
    public ElementCasing VariableCasing { get; init; } = ElementCasing.Lower;

    /// <summary>Casing for system tables (sys.*, information_schema.*)</summary>
    public ElementCasing SystemTableCasing { get; init; } = ElementCasing.Lower;

    /// <summary>Casing for stored procedures and user-defined functions (preserve original)</summary>
    public ElementCasing StoredProcedureCasing { get; init; } = ElementCasing.None;

    /// <summary>Casing for user-defined functions (preserve original)</summary>
    public ElementCasing UserDefinedFunctionCasing { get; init; } = ElementCasing.None;

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

    /// <summary>Line ending style for output (Auto = detect from input, fallback to CRLF)</summary>
    public LineEnding LineEnding { get; init; } = LineEnding.Auto;
}

