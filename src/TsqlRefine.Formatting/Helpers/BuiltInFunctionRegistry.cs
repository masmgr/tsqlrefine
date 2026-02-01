using System.Collections.Frozen;

namespace TsqlRefine.Formatting.Helpers;

/// <summary>
/// Registry of T-SQL built-in functions for categorization.
/// </summary>
internal static class BuiltInFunctionRegistry
{
    /// <summary>
    /// Common T-SQL built-in functions (aggregate, string, date, system, etc.)
    /// </summary>
    public static readonly FrozenSet<string> Functions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        // Aggregate functions
        "COUNT", "SUM", "AVG", "MIN", "MAX", "STDEV", "STDEVP", "VAR", "VARP",
        "CHECKSUM_AGG", "COUNT_BIG", "GROUPING", "GROUPING_ID",

        // String functions
        "LEN", "SUBSTRING", "LEFT", "RIGHT", "UPPER", "LOWER", "TRIM", "LTRIM", "RTRIM",
        "REPLACE", "REPLICATE", "REVERSE", "STUFF", "CHARINDEX", "PATINDEX",
        "CONCAT", "CONCAT_WS", "STRING_AGG", "FORMAT", "QUOTENAME", "SOUNDEX",
        "SPACE", "STR", "UNICODE", "ASCII", "CHAR", "NCHAR",

        // Date/time functions
        "GETDATE", "GETUTCDATE", "SYSDATETIME", "SYSUTCDATETIME", "SYSDATETIMEOFFSET",
        "CURRENT_TIMESTAMP", "DATEADD", "DATEDIFF", "DATEDIFF_BIG", "DATEFROMPARTS",
        "DATETIME2FROMPARTS", "DATETIMEFROMPARTS", "DATETIMEOFFSETFROMPARTS",
        "SMALLDATETIMEFROMPARTS", "TIMEFROMPARTS", "DATENAME", "DATEPART",
        "DAY", "MONTH", "YEAR", "EOMONTH", "ISDATE",

        // Conversion functions
        "CAST", "CONVERT", "PARSE", "TRY_CAST", "TRY_CONVERT", "TRY_PARSE",

        // Null handling functions
        "ISNULL", "COALESCE", "NULLIF",

        // Mathematical functions
        "ABS", "CEILING", "FLOOR", "ROUND", "POWER", "SQRT", "SQUARE", "EXP", "LOG", "LOG10",
        "SIGN", "RAND", "SIN", "COS", "TAN", "ASIN", "ACOS", "ATAN", "ATN2", "DEGREES", "RADIANS",

        // Ranking functions
        "ROW_NUMBER", "RANK", "DENSE_RANK", "NTILE", "PERCENT_RANK", "CUME_DIST",

        // System functions
        "@@IDENTITY", "@@ROWCOUNT", "@@ERROR", "@@TRANCOUNT", "@@VERSION",
        "SCOPE_IDENTITY", "IDENT_CURRENT", "IDENT_INCR", "IDENT_SEED",
        "NEWID", "NEWSEQUENTIALID", "SERVERPROPERTY", "CONNECTIONPROPERTY",
        "CURRENT_USER", "SESSION_USER", "SYSTEM_USER", "USER_NAME", "SUSER_NAME",
        "DB_ID", "DB_NAME", "OBJECT_ID", "OBJECT_NAME", "TYPE_ID", "TYPE_NAME",
        "SCHEMA_ID", "SCHEMA_NAME", "COL_LENGTH", "COL_NAME", "COLUMNPROPERTY",

        // JSON functions (SQL Server 2016+)
        "ISJSON", "JSON_VALUE", "JSON_QUERY", "JSON_MODIFY",
        "OPENJSON", "FOR JSON",

        // XML functions
        "OPENXML", "FOR XML",

        // Other functions
        "IIF", "CHOOSE", "CASE", "HASHBYTES", "CHECKSUM", "BINARY_CHECKSUM"
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Built-in functions that do not require parentheses (niladic functions).
    /// These are treated as functions even without following parentheses.
    /// </summary>
    public static readonly FrozenSet<string> ParenthesisFreeFunctions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "CURRENT_TIMESTAMP", "CURRENT_USER", "SESSION_USER", "SYSTEM_USER", "USER"
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Checks if the given text is a recognized built-in function.
    /// </summary>
    public static bool IsBuiltInFunction(string text) =>
        Functions.Contains(text);

    /// <summary>
    /// Checks if the given text is a parenthesis-free function.
    /// </summary>
    public static bool IsParenthesisFreeFunction(string text) =>
        ParenthesisFreeFunctions.Contains(text);
}
