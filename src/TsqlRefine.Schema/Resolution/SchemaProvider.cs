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

    /// <inheritdoc />
    public SchemaPrimaryKeyInfo? GetPrimaryKey(ResolvedTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        var tableSchema = _resolver.GetTableSchema(table);
        return tableSchema?.PrimaryKey?.ToDto();
    }

    /// <inheritdoc />
    public IReadOnlyList<SchemaUniqueConstraintInfo> GetUniqueConstraints(ResolvedTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        var tableSchema = _resolver.GetTableSchema(table);
        if (tableSchema is null)
        {
            return [];
        }

        var result = new List<SchemaUniqueConstraintInfo>();

        if (tableSchema.UniqueConstraints is not null)
        {
            foreach (var uc in tableSchema.UniqueConstraints)
            {
                result.Add(uc.ToDto());
            }
        }

        if (tableSchema.Indexes is not null)
        {
            foreach (var idx in tableSchema.Indexes)
            {
                if (idx.IsUnique)
                {
                    result.Add(idx.ToUniqueDto());
                }
            }
        }

        return result;
    }

    /// <inheritdoc />
    public IReadOnlyList<SchemaForeignKeyInfo> GetForeignKeys(ResolvedTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        var tableSchema = _resolver.GetTableSchema(table);
        if (tableSchema?.ForeignKeys is null)
        {
            return [];
        }

        var result = new List<SchemaForeignKeyInfo>(tableSchema.ForeignKeys.Count);
        foreach (var fk in tableSchema.ForeignKeys)
        {
            var targetTable = _resolver.ResolveTable(null, fk.TargetSchema, fk.TargetTable);
            if (targetTable is null)
            {
                continue;
            }

            result.Add(fk.ToDto(table, targetTable));
        }

        return result;
    }

    /// <inheritdoc />
    public IReadOnlyList<SchemaForeignKeyInfo> GetReferencingForeignKeys(ResolvedTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        var refs = _resolver.GetReferencingForeignKeys(table);
        if (refs.Count == 0)
        {
            return [];
        }

        var result = new List<SchemaForeignKeyInfo>(refs.Count);
        foreach (var (sourceTableSchema, fk) in refs)
        {
            var sourceTable = _resolver.ResolveTable(null, sourceTableSchema.SchemaName, sourceTableSchema.Name);
            if (sourceTable is null)
            {
                continue;
            }

            result.Add(fk.ToDto(sourceTable, table));
        }

        return result;
    }

    /// <inheritdoc />
    public bool IsUniqueColumnSet(ResolvedTable table, IReadOnlyList<string> columnNames)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(columnNames);

        if (columnNames.Count == 0)
        {
            return false;
        }

        var tableSchema = _resolver.GetTableSchema(table);
        if (tableSchema is null)
        {
            return false;
        }

        // Check primary key
        if (tableSchema.PrimaryKey is not null && IsSubsetOf(tableSchema.PrimaryKey.Columns, columnNames))
        {
            return true;
        }

        // Check unique constraints
        if (tableSchema.UniqueConstraints is not null)
        {
            foreach (var uc in tableSchema.UniqueConstraints)
            {
                if (IsSubsetOf(uc.Columns, columnNames))
                {
                    return true;
                }
            }
        }

        // Check unique indexes
        if (tableSchema.Indexes is not null)
        {
            foreach (var idx in tableSchema.Indexes)
            {
                if (idx.IsUnique && IsSubsetOf(idx.Columns, columnNames))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <inheritdoc />
    public JoinCardinality EstimateJoinCardinality(
        ResolvedTable leftTable, IReadOnlyList<string> leftColumns,
        ResolvedTable rightTable, IReadOnlyList<string> rightColumns)
    {
        ArgumentNullException.ThrowIfNull(leftTable);
        ArgumentNullException.ThrowIfNull(leftColumns);
        ArgumentNullException.ThrowIfNull(rightTable);
        ArgumentNullException.ThrowIfNull(rightColumns);

        var leftUnique = IsUniqueColumnSet(leftTable, leftColumns);
        var rightUnique = IsUniqueColumnSet(rightTable, rightColumns);

        return (leftUnique, rightUnique) switch
        {
            (true, true) => JoinCardinality.OneToOne,
            (true, false) => JoinCardinality.OneToMany,
            (false, true) => JoinCardinality.ManyToOne,
            (false, false) => JoinCardinality.ManyToMany,
        };
    }

    /// <summary>
    /// Checks whether all columns in <paramref name="constraintColumns"/> are present
    /// in the <paramref name="queryColumns"/> set (case-insensitive).
    /// This determines if the query columns cover (are a superset of) the constraint.
    /// </summary>
    private static bool IsSubsetOf(IReadOnlyList<string> constraintColumns, IReadOnlyList<string> queryColumns)
    {
        foreach (var constraintCol in constraintColumns)
        {
            var found = false;
            foreach (var queryCol in queryColumns)
            {
                if (string.Equals(constraintCol, queryCol, StringComparison.OrdinalIgnoreCase))
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                return false;
            }
        }

        return true;
    }
}
