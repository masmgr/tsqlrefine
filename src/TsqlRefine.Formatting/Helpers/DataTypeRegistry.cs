using System.Collections.Frozen;

namespace TsqlRefine.Formatting.Helpers;

/// <summary>
/// Registry of T-SQL data types for categorization.
/// </summary>
internal static class DataTypeRegistry
{
    /// <summary>
    /// T-SQL data type keywords.
    /// </summary>
    public static readonly FrozenSet<string> DataTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        // Exact numeric types
        "BIT", "TINYINT", "SMALLINT", "INT", "INTEGER", "BIGINT", "DECIMAL", "NUMERIC", "MONEY", "SMALLMONEY",

        // Approximate numeric types
        "FLOAT", "REAL",

        // Date and time types
        "DATE", "TIME", "DATETIME", "DATETIME2", "DATETIMEOFFSET", "SMALLDATETIME",

        // Character string types
        "CHAR", "VARCHAR", "TEXT",

        // Unicode character string types
        "NCHAR", "NVARCHAR", "NTEXT",

        // Binary string types
        "BINARY", "VARBINARY", "IMAGE",

        // Other data types
        "UNIQUEIDENTIFIER", "XML", "SQL_VARIANT", "CURSOR", "TABLE", "HIERARCHYID",
        "GEOMETRY", "GEOGRAPHY", "ROWVERSION", "TIMESTAMP"
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Checks if the given text is a recognized data type.
    /// </summary>
    public static bool IsDataType(string text) =>
        DataTypes.Contains(text);
}
