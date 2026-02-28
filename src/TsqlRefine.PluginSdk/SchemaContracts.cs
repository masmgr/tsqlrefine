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
/// Represents a foreign key relationship between tables (PluginSdk DTO).
/// </summary>
/// <param name="Name">The foreign key constraint name.</param>
/// <param name="SourceTable">The table that contains the foreign key columns.</param>
/// <param name="SourceColumns">The column names in the source (referencing) table.</param>
/// <param name="TargetTable">The table that is referenced by the foreign key.</param>
/// <param name="TargetColumns">The column names in the target (referenced) table.</param>
public sealed record SchemaForeignKeyInfo(
    string Name,
    ResolvedTable SourceTable,
    IReadOnlyList<string> SourceColumns,
    ResolvedTable TargetTable,
    IReadOnlyList<string> TargetColumns
);

/// <summary>
/// Represents a primary key constraint (PluginSdk DTO).
/// </summary>
/// <param name="Columns">The column names that make up the primary key.</param>
/// <param name="IsClustered">Whether the primary key is clustered.</param>
public sealed record SchemaPrimaryKeyInfo(
    IReadOnlyList<string> Columns,
    bool IsClustered
);

/// <summary>
/// Represents a unique constraint or unique index (PluginSdk DTO).
/// Combines UNIQUE constraints and unique indexes into a unified view.
/// </summary>
/// <param name="Name">The constraint or index name.</param>
/// <param name="Columns">The column names that make up the unique constraint.</param>
public sealed record SchemaUniqueConstraintInfo(
    string Name,
    IReadOnlyList<string> Columns
);

/// <summary>
/// Describes the cardinality of a JOIN relationship between two tables.
/// </summary>
public enum JoinCardinality
{
    /// <summary>Both sides are unique: each row matches at most one row.</summary>
    OneToOne,

    /// <summary>Left side is unique, right side may have duplicates.</summary>
    OneToMany,

    /// <summary>Right side is unique, left side may have duplicates.</summary>
    ManyToOne,

    /// <summary>Neither side is unique: both may have duplicates.</summary>
    ManyToMany,

    /// <summary>Cardinality cannot be determined from available schema information.</summary>
    Unknown
}

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

    // --- ER relationship query methods (default implementations for backward compatibility) ---

    /// <summary>
    /// Gets the primary key information for a table.
    /// </summary>
    /// <param name="table">The resolved table.</param>
    /// <returns>The primary key info, or null if the table has no primary key.</returns>
    SchemaPrimaryKeyInfo? GetPrimaryKey(ResolvedTable table) => null;

    /// <summary>
    /// Gets all unique constraints and unique indexes for a table (unified view).
    /// </summary>
    /// <param name="table">The resolved table.</param>
    /// <returns>All unique constraints and unique indexes on the table.</returns>
    IReadOnlyList<SchemaUniqueConstraintInfo> GetUniqueConstraints(ResolvedTable table) => [];

    /// <summary>
    /// Gets all foreign keys defined on a table (where this table is the source/referencing table).
    /// </summary>
    /// <param name="table">The resolved table.</param>
    /// <returns>Foreign keys originating from this table.</returns>
    IReadOnlyList<SchemaForeignKeyInfo> GetForeignKeys(ResolvedTable table) => [];

    /// <summary>
    /// Gets all foreign keys that reference this table (where this table is the target/referenced table).
    /// </summary>
    /// <param name="table">The resolved table.</param>
    /// <returns>Foreign keys from other tables that reference this table.</returns>
    IReadOnlyList<SchemaForeignKeyInfo> GetReferencingForeignKeys(ResolvedTable table) => [];

    /// <summary>
    /// Determines whether the specified column set has a uniqueness guarantee
    /// (via primary key, unique constraint, or unique index).
    /// </summary>
    /// <param name="table">The resolved table.</param>
    /// <param name="columnNames">The column names to check for uniqueness.</param>
    /// <returns>True if the column set is guaranteed unique; false otherwise.</returns>
    bool IsUniqueColumnSet(ResolvedTable table, IReadOnlyList<string> columnNames) => false;

    /// <summary>
    /// Estimates the JOIN cardinality between two tables based on the uniqueness of the JOIN columns.
    /// </summary>
    /// <param name="leftTable">The left table in the JOIN.</param>
    /// <param name="leftColumns">The JOIN columns from the left table.</param>
    /// <param name="rightTable">The right table in the JOIN.</param>
    /// <param name="rightColumns">The JOIN columns from the right table.</param>
    /// <returns>The estimated cardinality of the JOIN relationship.</returns>
    JoinCardinality EstimateJoinCardinality(
        ResolvedTable leftTable, IReadOnlyList<string> leftColumns,
        ResolvedTable rightTable, IReadOnlyList<string> rightColumns) => JoinCardinality.Unknown;
}

// =============================================================================
// Relation Deviation Contracts
// =============================================================================

/// <summary>
/// Classification of how unusual a JOIN pattern is compared to the dominant pattern for a table pair.
/// </summary>
public enum RelationDeviationLevel
{
    /// <summary>This is the dominant (most frequent) pattern.</summary>
    Dominant,

    /// <summary>Common enough to not be flagged.</summary>
    Common,

    /// <summary>Occurs below the rare threshold.</summary>
    Rare,

    /// <summary>Occurs below the very-rare threshold.</summary>
    VeryRare,

    /// <summary>Structurally different from the dominant pattern.</summary>
    Structural,

    /// <summary>Insufficient data to classify.</summary>
    InsufficientData,
}

/// <summary>
/// Describes a structural difference between a JOIN pattern and the dominant pattern.
/// </summary>
public enum RelationStructuralDiff
{
    /// <summary>Different number of key columns.</summary>
    DifferentKeyCount,

    /// <summary>Function call presence mismatch.</summary>
    FunctionPresenceMismatch,

    /// <summary>OR presence mismatch.</summary>
    OrPresenceMismatch,

    /// <summary>Range condition presence mismatch.</summary>
    RangePresenceMismatch,

    /// <summary>Different JOIN type.</summary>
    DifferentJoinType,
}

/// <summary>
/// Deviation analysis result for a single JOIN pattern within a table pair (PluginSdk DTO).
/// </summary>
/// <param name="JoinType">The JOIN type (INNER, LEFT, etc.).</param>
/// <param name="ColumnPairDescriptions">Descriptions of column pairs (e.g., "Id=UserId").</param>
/// <param name="OccurrenceCount">Number of times this pattern was observed.</param>
/// <param name="Ratio">Occurrence ratio within the table pair (0.0–1.0).</param>
/// <param name="DominantRatio">Ratio of the dominant pattern.</param>
/// <param name="Gap">Distance from the dominant ratio.</param>
/// <param name="Rank">1-based rank among patterns (1 = most common).</param>
/// <param name="Level">Classification level of this deviation.</param>
/// <param name="StructuralDiffs">Structural differences from the dominant pattern.</param>
public sealed record RelationPatternDeviation(
    string JoinType,
    IReadOnlyList<string> ColumnPairDescriptions,
    int OccurrenceCount,
    double Ratio,
    double DominantRatio,
    double Gap,
    int Rank,
    RelationDeviationLevel Level,
    IReadOnlyList<RelationStructuralDiff> StructuralDiffs
);

/// <summary>
/// Deviation analysis summary for a single table pair (PluginSdk DTO).
/// </summary>
/// <param name="LeftSchema">Left table schema.</param>
/// <param name="LeftTable">Left table name.</param>
/// <param name="RightSchema">Right table schema.</param>
/// <param name="RightTable">Right table name.</param>
/// <param name="Total">Total occurrence count across all patterns.</param>
/// <param name="PatternCount">Number of distinct JOIN patterns.</param>
/// <param name="Deviations">Per-pattern deviation analysis results.</param>
public sealed record RelationTablePairSummary(
    string LeftSchema,
    string LeftTable,
    string RightSchema,
    string RightTable,
    int Total,
    int PatternCount,
    IReadOnlyList<RelationPatternDeviation> Deviations
);

/// <summary>
/// Provides JOIN pattern deviation information for deviation-aware analysis rules.
/// When configured, enables rules that detect unusual JOIN patterns across SQL files.
/// </summary>
public interface IRelationDeviationProvider
{
    /// <summary>
    /// Gets whether any deviation data is available.
    /// </summary>
    bool HasData { get; }

    /// <summary>
    /// Gets the total number of table pairs analyzed.
    /// </summary>
    int TablePairCount { get; }

    /// <summary>
    /// Gets the deviation summary for a specific table pair, using canonicalized (lexicographic) ordering.
    /// </summary>
    /// <param name="leftSchema">Left table schema.</param>
    /// <param name="leftTable">Left table name.</param>
    /// <param name="rightSchema">Right table schema.</param>
    /// <param name="rightTable">Right table name.</param>
    /// <returns>The table pair summary, or null if no data exists for this pair.</returns>
    RelationTablePairSummary? GetTablePairSummary(
        string leftSchema, string leftTable,
        string rightSchema, string rightTable);

    /// <summary>
    /// Gets all table pair summaries.
    /// </summary>
    IReadOnlyList<RelationTablePairSummary> GetAllSummaries();
}
