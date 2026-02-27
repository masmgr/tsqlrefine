namespace TsqlRefine.Schema.Relations;

/// <summary>
/// Root model for a relation profile containing aggregated JOIN patterns from SQL files.
/// </summary>
/// <param name="Metadata">Metadata about the profile generation.</param>
/// <param name="Relations">Aggregated table relations.</param>
public sealed record RelationProfile(
    RelationProfileMetadata Metadata,
    IReadOnlyList<TableRelation> Relations
);

/// <summary>
/// Metadata about the relation profile generation.
/// </summary>
/// <param name="GeneratedAt">ISO 8601 timestamp of when the profile was generated.</param>
/// <param name="FileCount">Number of SQL files analyzed.</param>
/// <param name="TotalJoinCount">Total number of JOIN occurrences found.</param>
/// <param name="ContentHash">SHA-256 hash of the serialized relation content.</param>
public sealed record RelationProfileMetadata(
    string GeneratedAt,
    int FileCount,
    int TotalJoinCount,
    string ContentHash
);

/// <summary>
/// Aggregated JOIN patterns between a pair of tables.
/// Tables are ordered lexicographically (schema.table) so that A→B and B→A are combined.
/// </summary>
/// <param name="LeftSchema">Schema name of the left table.</param>
/// <param name="LeftTable">Name of the left table.</param>
/// <param name="RightSchema">Schema name of the right table.</param>
/// <param name="RightTable">Name of the right table.</param>
/// <param name="Patterns">Distinct JOIN patterns observed between these tables.</param>
public sealed record TableRelation(
    string LeftSchema,
    string LeftTable,
    string RightSchema,
    string RightTable,
    IReadOnlyList<JoinPattern> Patterns
);

/// <summary>
/// A distinct JOIN pattern between two tables.
/// </summary>
/// <param name="JoinType">The JOIN type: "INNER", "LEFT", "RIGHT", "FULL", or "CROSS".</param>
/// <param name="ColumnPairs">Column pairs used in the ON clause.</param>
/// <param name="OccurrenceCount">Number of times this exact pattern appears across all files.</param>
/// <param name="SourceFiles">File paths containing this pattern.</param>
public sealed record JoinPattern(
    string JoinType,
    IReadOnlyList<ColumnPair> ColumnPairs,
    int OccurrenceCount,
    IReadOnlyList<string> SourceFiles
);

/// <summary>
/// A pair of columns used in a JOIN condition (ON clause equality).
/// </summary>
/// <param name="LeftColumn">Column name from the left table.</param>
/// <param name="RightColumn">Column name from the right table.</param>
public sealed record ColumnPair(
    string LeftColumn,
    string RightColumn
);
