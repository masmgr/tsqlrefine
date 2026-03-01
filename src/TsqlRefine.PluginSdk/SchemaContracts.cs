namespace TsqlRefine.PluginSdk;

/// <summary>
/// Categorizes SQL Server data types into logical groups for type compatibility analysis.
/// </summary>
public enum SchemaTypeCategory
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

/// <summary>
/// Data type information for a resolved column (PluginSdk DTO).
/// </summary>
/// <param name="TypeName">The SQL Server type name (e.g., "int", "nvarchar", "decimal").</param>
/// <param name="Category">The type category for compatibility analysis.</param>
/// <param name="MaxLength">Maximum length in bytes. -1 indicates MAX. Null for types without length.</param>
/// <param name="Precision">Numeric precision. Null for non-numeric types.</param>
/// <param name="Scale">Numeric scale. Null for non-numeric types.</param>
public sealed record SchemaTypeInfo(
    string TypeName,
    SchemaTypeCategory Category,
    int? MaxLength = null,
    int? Precision = null,
    int? Scale = null
);

/// <summary>
/// Information about a resolved column (PluginSdk DTO).
/// </summary>
/// <param name="Name">The column name.</param>
/// <param name="Type">The column's data type information.</param>
/// <param name="IsNullable">Whether the column allows NULL values.</param>
/// <param name="IsIdentity">Whether the column is an identity column.</param>
/// <param name="IsComputed">Whether the column is a computed column.</param>
public sealed record SchemaColumnInfo(
    string Name,
    SchemaTypeInfo Type,
    bool IsNullable,
    bool IsIdentity = false,
    bool IsComputed = false
);

/// <summary>
/// Represents a resolved table or view reference.
/// </summary>
/// <param name="DatabaseName">The database name.</param>
/// <param name="SchemaName">The schema name (e.g., "dbo").</param>
/// <param name="TableName">The table or view name.</param>
/// <param name="IsView">Whether this is a view (true) or a table (false).</param>
public sealed record ResolvedTable(
    string DatabaseName,
    string SchemaName,
    string TableName,
    bool IsView
);

/// <summary>
/// Represents a resolved column reference.
/// </summary>
/// <param name="Table">The table containing the column.</param>
/// <param name="Column">The column information.</param>
public sealed record ResolvedColumn(
    ResolvedTable Table,
    SchemaColumnInfo Column
);

/// <summary>
/// Metadata about a schema snapshot.
/// </summary>
/// <param name="GeneratedAt">ISO 8601 timestamp of when the snapshot was generated.</param>
/// <param name="ServerName">The SQL Server instance name.</param>
/// <param name="DatabaseName">The database name.</param>
/// <param name="CompatLevel">The SQL Server compatibility level.</param>
/// <param name="ContentHash">SHA-256 hash of the serialized snapshot content.</param>
public sealed record SchemaSnapshotMetadata(
    string GeneratedAt,
    string ServerName,
    string DatabaseName,
    int CompatLevel,
    string ContentHash
);

/// <summary>
/// Provides schema information for schema-aware static analysis rules.
/// Schema rules use this interface to resolve table and column references.
/// </summary>
public interface ISchemaProvider
{
    /// <summary>
    /// Resolves a table or view reference using 1, 2, or 3-part naming.
    /// </summary>
    /// <param name="database">The database name, or null for default.</param>
    /// <param name="schema">The schema name, or null for default.</param>
    /// <param name="name">The table or view name.</param>
    /// <returns>The resolved table, or null if not found.</returns>
    ResolvedTable? ResolveTable(string? database, string? schema, string name);

    /// <summary>
    /// Resolves a column within a resolved table.
    /// </summary>
    /// <param name="table">The resolved table to search within.</param>
    /// <param name="columnName">The column name to resolve.</param>
    /// <returns>The resolved column, or null if not found.</returns>
    ResolvedColumn? ResolveColumn(ResolvedTable table, string columnName);

    /// <summary>
    /// Gets all columns for a resolved table.
    /// </summary>
    /// <param name="table">The resolved table.</param>
    /// <returns>All columns in the table.</returns>
    IReadOnlyList<SchemaColumnInfo> GetColumns(ResolvedTable table);

    /// <summary>
    /// Gets the default schema name used for unqualified table references.
    /// </summary>
    string DefaultSchema { get; }

    /// <summary>
    /// Gets the metadata about the schema snapshot.
    /// </summary>
    SchemaSnapshotMetadata Metadata { get; }
}
