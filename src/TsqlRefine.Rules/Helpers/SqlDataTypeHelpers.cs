using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace TsqlRefine.Rules.Helpers;

/// <summary>
/// Utilities for SQL data type analysis and classification.
/// </summary>
public static class SqlDataTypeHelpers
{
    /// <summary>
    /// Determines if the data type is a non-Unicode string type (VARCHAR, CHAR, TEXT).
    /// </summary>
    public static bool IsNonUnicodeStringType(SqlDataTypeReference dataType)
    {
        return dataType.SqlDataTypeOption is
            SqlDataTypeOption.VarChar or
            SqlDataTypeOption.Char or
            SqlDataTypeOption.Text;
    }

    /// <summary>
    /// Determines if the data type is encoding-sensitive (may lose data during conversions).
    /// Includes non-Unicode string types and binary types.
    /// </summary>
    public static bool IsEncodingSensitiveType(SqlDataTypeReference dataType)
    {
        return dataType.SqlDataTypeOption is
            SqlDataTypeOption.VarChar or
            SqlDataTypeOption.Char or
            SqlDataTypeOption.Text or
            SqlDataTypeOption.Binary or
            SqlDataTypeOption.VarBinary or
            SqlDataTypeOption.Image;
    }

    /// <summary>
    /// Gets the Unicode equivalent type name for a non-Unicode string type.
    /// </summary>
    /// <param name="option">The SQL data type option.</param>
    /// <returns>The lowercase Unicode type name, or null if not applicable.</returns>
    public static string? GetUnicodeEquivalent(SqlDataTypeOption option)
    {
        return option switch
        {
            SqlDataTypeOption.VarChar => "nvarchar",
            SqlDataTypeOption.Char => "nchar",
            SqlDataTypeOption.Text => "ntext",
            _ => null
        };
    }

    /// <summary>
    /// Checks if a string value contains any Unicode characters (characters outside ASCII range 0-127).
    /// </summary>
    public static bool ContainsUnicodeCharacters(string value)
    {
        return value.Any(c => c > 127);
    }
}
