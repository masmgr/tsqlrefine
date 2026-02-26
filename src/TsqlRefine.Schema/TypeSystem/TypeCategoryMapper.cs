using System.Collections.Frozen;

namespace TsqlRefine.Schema.TypeSystem;

/// <summary>
/// Maps SQL Server type names to their <see cref="TypeCategory"/>.
/// </summary>
public static class TypeCategoryMapper
{
    private static readonly FrozenDictionary<string, TypeCategory> TypeMap = new Dictionary<string, TypeCategory>(StringComparer.OrdinalIgnoreCase)
    {
        // Exact numeric
        ["bit"] = TypeCategory.ExactNumeric,
        ["tinyint"] = TypeCategory.ExactNumeric,
        ["smallint"] = TypeCategory.ExactNumeric,
        ["int"] = TypeCategory.ExactNumeric,
        ["bigint"] = TypeCategory.ExactNumeric,
        ["decimal"] = TypeCategory.ExactNumeric,
        ["numeric"] = TypeCategory.ExactNumeric,
        ["money"] = TypeCategory.ExactNumeric,
        ["smallmoney"] = TypeCategory.ExactNumeric,

        // Approximate numeric
        ["float"] = TypeCategory.ApproximateNumeric,
        ["real"] = TypeCategory.ApproximateNumeric,

        // String (non-Unicode)
        ["char"] = TypeCategory.AnsiString,
        ["varchar"] = TypeCategory.AnsiString,
        ["text"] = TypeCategory.AnsiString,

        // Unicode
        ["nchar"] = TypeCategory.UnicodeString,
        ["nvarchar"] = TypeCategory.UnicodeString,
        ["ntext"] = TypeCategory.UnicodeString,

        // DateTime
        ["date"] = TypeCategory.DateTime,
        ["time"] = TypeCategory.DateTime,
        ["datetime"] = TypeCategory.DateTime,
        ["datetime2"] = TypeCategory.DateTime,
        ["datetimeoffset"] = TypeCategory.DateTime,
        ["smalldatetime"] = TypeCategory.DateTime,

        // Binary
        ["binary"] = TypeCategory.Binary,
        ["varbinary"] = TypeCategory.Binary,
        ["image"] = TypeCategory.Binary,

        // Guid
        ["uniqueidentifier"] = TypeCategory.UniqueIdentifier,

        // Xml
        ["xml"] = TypeCategory.Xml,

        // Spatial
        ["geography"] = TypeCategory.Spatial,
        ["geometry"] = TypeCategory.Spatial,

        // Other
        ["sql_variant"] = TypeCategory.Other,
        ["hierarchyid"] = TypeCategory.Other,
        ["cursor"] = TypeCategory.Other,
        ["table"] = TypeCategory.Other,
        ["timestamp"] = TypeCategory.Other,
        ["rowversion"] = TypeCategory.Other,
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Returns the <see cref="TypeCategory"/> for a given SQL Server type name.
    /// Returns <see cref="TypeCategory.Other"/> for unrecognized type names.
    /// </summary>
    /// <param name="typeName">The SQL Server type name (case-insensitive).</param>
    /// <returns>The corresponding type category.</returns>
    public static TypeCategory FromTypeName(string typeName)
    {
        ArgumentNullException.ThrowIfNull(typeName);
        return TypeMap.GetValueOrDefault(typeName, TypeCategory.Other);
    }
}
