using TsqlRefine.Schema.TypeSystem;

namespace TsqlRefine.Schema.Model;

/// <summary>
/// Represents the data type information for a SQL Server column.
/// </summary>
/// <param name="TypeName">The SQL Server type name (e.g., "int", "nvarchar", "decimal").</param>
/// <param name="Category">The type category for compatibility analysis.</param>
/// <param name="MaxLength">Maximum length in bytes. -1 indicates MAX. Null for types without length.</param>
/// <param name="Precision">Numeric precision. Null for non-numeric types.</param>
/// <param name="Scale">Numeric scale. Null for non-numeric types.</param>
public sealed record SqlTypeInfo(
    string TypeName,
    TypeCategory Category,
    int? MaxLength = null,
    int? Precision = null,
    int? Scale = null
);
