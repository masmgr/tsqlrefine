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
);
