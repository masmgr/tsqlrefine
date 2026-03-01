using TsqlRefine.PluginSdk;

namespace TsqlRefine.Schema.Resolution;

/// <summary>
/// Combines <see cref="ISchemaProvider"/> and an optional <see cref="IRelationDeviationProvider"/>
/// into a single <see cref="ISchemaContext"/> for use by analysis rules.
/// </summary>
public sealed class SchemaContext : ISchemaContext
{
    private readonly ISchemaProvider _schema;

    /// <summary>
    /// Creates a new <see cref="SchemaContext"/> wrapping the given providers.
    /// </summary>
    /// <param name="schema">The schema provider to delegate to.</param>
    /// <param name="relationDeviations">Optional relation deviation provider.</param>
    public SchemaContext(ISchemaProvider schema, IRelationDeviationProvider? relationDeviations = null)
    {
        ArgumentNullException.ThrowIfNull(schema);
        _schema = schema;
        RelationDeviations = relationDeviations;
    }

    /// <inheritdoc />
    public IRelationDeviationProvider? RelationDeviations { get; }

    /// <inheritdoc />
    public string DefaultSchema => _schema.DefaultSchema;

    /// <inheritdoc />
    public SchemaSnapshotMetadata Metadata => _schema.Metadata;

    /// <inheritdoc />
    public ResolvedTable? ResolveTable(string? database, string? schema, string name)
        => _schema.ResolveTable(database, schema, name);

    /// <inheritdoc />
    public ResolvedColumn? ResolveColumn(ResolvedTable table, string columnName)
        => _schema.ResolveColumn(table, columnName);

    /// <inheritdoc />
    public IReadOnlyList<SchemaColumnInfo> GetColumns(ResolvedTable table)
        => _schema.GetColumns(table);

    /// <inheritdoc />
    public SchemaPrimaryKeyInfo? GetPrimaryKey(ResolvedTable table)
        => _schema.GetPrimaryKey(table);

    /// <inheritdoc />
    public IReadOnlyList<SchemaUniqueConstraintInfo> GetUniqueConstraints(ResolvedTable table)
        => _schema.GetUniqueConstraints(table);

    /// <inheritdoc />
    public IReadOnlyList<SchemaForeignKeyInfo> GetForeignKeys(ResolvedTable table)
        => _schema.GetForeignKeys(table);

    /// <inheritdoc />
    public IReadOnlyList<SchemaForeignKeyInfo> GetReferencingForeignKeys(ResolvedTable table)
        => _schema.GetReferencingForeignKeys(table);

    /// <inheritdoc />
    public bool IsUniqueColumnSet(ResolvedTable table, IReadOnlyList<string> columnNames)
        => _schema.IsUniqueColumnSet(table, columnNames);

    /// <inheritdoc />
    public JoinCardinality EstimateJoinCardinality(
        ResolvedTable leftTable, IReadOnlyList<string> leftColumns,
        ResolvedTable rightTable, IReadOnlyList<string> rightColumns)
        => _schema.EstimateJoinCardinality(leftTable, leftColumns, rightTable, rightColumns);
}
