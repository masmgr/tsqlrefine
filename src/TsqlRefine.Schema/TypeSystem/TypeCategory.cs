namespace TsqlRefine.Schema.TypeSystem;

/// <summary>
/// Categorizes SQL Server data types into logical groups for type compatibility analysis.
/// </summary>
public enum TypeCategory
{
    /// <summary>Exact numeric types: int, bigint, smallint, tinyint, decimal, numeric, money, smallmoney, bit.</summary>
    ExactNumeric,

    /// <summary>Approximate numeric types: float, real.</summary>
    ApproximateNumeric,

    /// <summary>Non-Unicode string types: varchar, char, text.</summary>
    AnsiString,

    /// <summary>Unicode string types: nvarchar, nchar, ntext.</summary>
    UnicodeString,

    /// <summary>Date and time types: datetime, datetime2, date, time, datetimeoffset, smalldatetime.</summary>
    DateTime,

    /// <summary>Binary types: binary, varbinary, image.</summary>
    Binary,

    /// <summary>Globally unique identifier: uniqueidentifier.</summary>
    UniqueIdentifier,

    /// <summary>XML data type.</summary>
    Xml,

    /// <summary>Spatial types: geography, geometry.</summary>
    Spatial,

    /// <summary>Other types: sql_variant, hierarchyid, cursor, table, etc.</summary>
    Other
}
