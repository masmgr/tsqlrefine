using TsqlRefine.PluginSdk;
using TsqlRefine.Schema.Model;

namespace TsqlRefine.Schema.Resolution;

/// <summary>
/// Implements <see cref="ISchemaProvider"/> using a <see cref="SchemaSnapshot"/>.
/// Resolves table and column references against the snapshot data.
/// </summary>
public sealed class SchemaProvider : ISchemaProvider
{
    private readonly NameResolver _resolver;
    private readonly SchemaSnapshotMetadata _metadata;

    /// <summary>
    /// Creates a new <see cref="SchemaProvider"/> from a schema snapshot.
    /// </summary>
    /// <param name="snapshot">The schema snapshot to resolve against.</param>
    /// <param name="defaultSchema">The default schema name for unqualified references (defaults to "dbo").</param>
    public SchemaProvider(SchemaSnapshot snapshot, string defaultSchema = "dbo")
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _resolver = new NameResolver(snapshot, defaultSchema);
        _metadata = snapshot.Metadata.ToDto();
        DefaultSchema = defaultSchema;
    }

    /// <inheritdoc />
    public string DefaultSchema { get; }

    /// <inheritdoc />
    public SchemaSnapshotMetadata Metadata => _metadata;

    /// <inheritdoc />
    public ResolvedTable? ResolveTable(string? database, string? schema, string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return _resolver.ResolveTable(database, schema, name);
    }

    /// <inheritdoc />
    public ResolvedColumn? ResolveColumn(ResolvedTable table, string columnName)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(columnName);
        return _resolver.ResolveColumn(table, columnName);
    }

    /// <inheritdoc />
    public IReadOnlyList<SchemaColumnInfo> GetColumns(ResolvedTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        return _resolver.GetColumns(table);
    }
}
