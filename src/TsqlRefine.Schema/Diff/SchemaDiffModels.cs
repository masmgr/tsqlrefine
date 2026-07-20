namespace TsqlRefine.Schema.Diff;

/// <summary>Kind of structural change found between two schema snapshots.</summary>
public enum SchemaChangeKind
{
    DatabaseAdded,
    DatabaseRemoved,
    TableAdded,
    TableRemoved,
    ViewAdded,
    ViewRemoved,
    ColumnAdded,
    ColumnRemoved,
    ColumnTypeChanged,
    ColumnNullabilityChanged
}

/// <summary>A single structural change between two schema snapshots.</summary>
public sealed record SchemaChange(
    SchemaChangeKind Kind,
    bool IsBreaking,
    string DatabaseName,
    string? SchemaName = null,
    string? ObjectName = null,
    string? ObjectKind = null,
    string? ColumnName = null,
    string? Before = null,
    string? After = null);

/// <summary>Ordered set of changes between two schema snapshots.</summary>
public sealed record SchemaDiff(IReadOnlyList<SchemaChange> Changes)
{
    /// <summary>Number of changes classified as breaking.</summary>
    public int BreakingChangeCount => Changes.Count(static change => change.IsBreaking);
}
