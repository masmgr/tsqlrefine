namespace TsqlRefine.Schema.Model;

/// <summary>
/// Represents a table or view definition with its columns and constraints.
/// </summary>
/// <param name="SchemaName">The schema name (e.g., "dbo").</param>
/// <param name="Name">The table or view name.</param>
/// <param name="Columns">The columns defined on this table or view.</param>
/// <param name="PrimaryKey">The primary key constraint, if any.</param>
/// <param name="UniqueConstraints">Unique constraints on this table.</param>
/// <param name="ForeignKeys">Foreign key relationships from this table.</param>
/// <param name="Indexes">Indexes defined on this table.</param>
public sealed record TableSchema(
    string SchemaName,
    string Name,
    IReadOnlyList<ColumnSchema> Columns,
    PrimaryKeyInfo? PrimaryKey = null,
    IReadOnlyList<UniqueConstraintInfo>? UniqueConstraints = null,
    IReadOnlyList<ForeignKeyInfo>? ForeignKeys = null,
    IReadOnlyList<IndexInfo>? Indexes = null
);
