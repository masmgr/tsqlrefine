using System.Collections.Frozen;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Schema.TypeSystem;

/// <summary>
/// Defines SQL Server type precedence rules for implicit conversion analysis.
/// Based on: https://learn.microsoft.com/en-us/sql/t-sql/data-types/data-type-precedence-transact-sql
/// </summary>
public static class ImplicitConversionRules
{
    /// <summary>
    /// Category precedence (higher number = higher precedence).
    /// When comparing across categories, the lower-precedence type is converted.
    /// </summary>
    /// <remarks>
    /// SQL Server precedence order (highest to lowest):
    /// sql_variant, xml, datetimeoffset, datetime2, datetime, smalldatetime, date, time,
    /// float, real, decimal, money, smallmoney, bigint, int, smallint, tinyint, bit,
    /// ntext, nvarchar, nchar, varchar, char, varbinary, binary, text, image, uniqueidentifier
    /// </remarks>
    private static readonly FrozenDictionary<SchemaTypeCategory, int> CategoryPrecedence =
        new Dictionary<SchemaTypeCategory, int>
        {
            [SchemaTypeCategory.Other] = 100,            // sql_variant etc.
            [SchemaTypeCategory.Xml] = 90,
            [SchemaTypeCategory.DateTime] = 80,
            [SchemaTypeCategory.ApproximateNumeric] = 70, // float, real
            [SchemaTypeCategory.ExactNumeric] = 60,       // decimal, int, etc.
            [SchemaTypeCategory.UnicodeString] = 50,      // nvarchar, nchar
            [SchemaTypeCategory.AnsiString] = 40,         // varchar, char
            [SchemaTypeCategory.Binary] = 30,
            [SchemaTypeCategory.UniqueIdentifier] = 20,
            [SchemaTypeCategory.Spatial] = 10,
        }.ToFrozenDictionary();

    /// <summary>
    /// Numeric type precedence within exact/approximate numeric categories.
    /// Higher number = higher precedence.
    /// </summary>
    private static readonly FrozenDictionary<string, int> NumericPrecedence =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["bit"] = 10,
            ["tinyint"] = 20,
            ["smallint"] = 30,
            ["int"] = 40,
            ["bigint"] = 50,
            ["smallmoney"] = 55,
            ["money"] = 60,
            ["decimal"] = 70,
            ["numeric"] = 70,
            ["real"] = 80,
            ["float"] = 90,
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// DateTime type precedence within the DateTime category.
    /// Higher number = higher precedence.
    /// </summary>
    private static readonly FrozenDictionary<string, int> DateTimePrecedenceMap =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["time"] = 10,
            ["date"] = 20,
            ["smalldatetime"] = 30,
            ["datetime"] = 40,
            ["datetime2"] = 50,
            ["datetimeoffset"] = 60,
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the category-level precedence. Higher values indicate higher precedence.
    /// </summary>
    internal static int GetCategoryPrecedence(SchemaTypeCategory category) =>
        CategoryPrecedence.GetValueOrDefault(category, 0);

    /// <summary>
    /// Gets the numeric type precedence within numeric categories.
    /// </summary>
    internal static int GetNumericPrecedence(string typeName) =>
        NumericPrecedence.GetValueOrDefault(typeName, 0);

    /// <summary>
    /// Gets the DateTime type precedence within the DateTime category.
    /// </summary>
    internal static int GetDateTimePrecedence(string typeName) =>
        DateTimePrecedenceMap.GetValueOrDefault(typeName, 0);
}
