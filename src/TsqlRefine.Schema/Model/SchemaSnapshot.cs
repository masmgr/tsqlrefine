using TsqlRefine.Schema.Snapshot;

namespace TsqlRefine.Schema.Model;

/// <summary>
/// Root model representing a complete schema snapshot, containing one or more database schemas.
/// </summary>
/// <param name="Metadata">Metadata about this snapshot (generation time, source, hash).</param>
/// <param name="Databases">The database schemas included in this snapshot.</param>
public sealed record SchemaSnapshot(
    SnapshotMetadata Metadata,
    IReadOnlyList<DatabaseSchema> Databases
);
