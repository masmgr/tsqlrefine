namespace TsqlRefine.Schema.Snapshot;

/// <summary>
/// Metadata about a schema snapshot, including when and from where it was generated.
/// </summary>
/// <param name="GeneratedAt">ISO 8601 timestamp of when the snapshot was generated.</param>
/// <param name="ServerName">The SQL Server instance name.</param>
/// <param name="DatabaseName">The database name.</param>
/// <param name="CompatLevel">The SQL Server compatibility level.</param>
/// <param name="ContentHash">SHA-256 hash of the serialized snapshot content (excluding metadata).</param>
public sealed record SnapshotMetadata(
    string GeneratedAt,
    string ServerName,
    string DatabaseName,
    int CompatLevel,
    string ContentHash
)
{
    /// <summary>Gets the database default collation used for identifier resolution.</summary>
    public string? DatabaseCollation { get; init; }

    /// <summary>Gets the locale identifier reported by COLLATIONPROPERTY for the database collation.</summary>
    public int? CollationLcid { get; init; }

    /// <summary>Gets the SQL Server comparison-style bitmask for the database collation.</summary>
    public int? CollationComparisonStyle { get; init; }
}
