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
    private readonly Dictionary<ResolvedTable, IReadOnlyList<SchemaUniqueConstraintInfo>> _uniqueConstraintsCache =
        new(ResolvedTableKeyComparer.Instance);
    private readonly Dictionary<ResolvedTable, IReadOnlyList<SchemaForeignKeyInfo>> _foreignKeysCache =
        new(ResolvedTableKeyComparer.Instance);
    private readonly Dictionary<ResolvedTable, IReadOnlyList<SchemaForeignKeyInfo>> _referencingForeignKeysCache =
        new(ResolvedTableKeyComparer.Instance);
    private readonly Dictionary<ResolvedTable, TableUniquenessLookup?> _uniquenessLookupCache =
        new(ResolvedTableKeyComparer.Instance);

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
        if (_uniqueConstraintsCache.TryGetValue(table, out var cached))
        {
            return cached;
        }

        var tableSchema = _resolver.GetTableSchema(table);
        if (tableSchema is null)
        {
            _uniqueConstraintsCache[table] = [];
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

        cached = result.Count == 0 ? [] : result;
        _uniqueConstraintsCache[table] = cached;
        return cached;
    }

    /// <inheritdoc />
    public IReadOnlyList<SchemaForeignKeyInfo> GetForeignKeys(ResolvedTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (_foreignKeysCache.TryGetValue(table, out var cached))
        {
            return cached;
        }

        var tableSchema = _resolver.GetTableSchema(table);
        if (tableSchema?.ForeignKeys is null)
        {
            _foreignKeysCache[table] = [];
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

        cached = result.Count == 0 ? [] : result;
        _foreignKeysCache[table] = cached;
        return cached;
    }

    /// <inheritdoc />
    public IReadOnlyList<SchemaForeignKeyInfo> GetReferencingForeignKeys(ResolvedTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (_referencingForeignKeysCache.TryGetValue(table, out var cached))
        {
            return cached;
        }

        var refs = _resolver.GetReferencingForeignKeys(table);
        if (refs.Count == 0)
        {
            _referencingForeignKeysCache[table] = [];
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

        cached = result.Count == 0 ? [] : result;
        _referencingForeignKeysCache[table] = cached;
        return cached;
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

        var uniquenessLookup = GetOrCreateUniquenessLookup(table);
        if (uniquenessLookup is null)
        {
            return false;
        }

        var queryColumns = new HashSet<string>(columnNames, StringComparer.OrdinalIgnoreCase);

        foreach (var uniqueColumnSet in uniquenessLookup.UniqueColumnSets)
        {
            if (uniqueColumnSet.IsSubsetOf(queryColumns))
            {
                return true;
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

    private TableUniquenessLookup? GetOrCreateUniquenessLookup(ResolvedTable table)
    {
        if (_uniquenessLookupCache.TryGetValue(table, out var cached))
        {
            return cached;
        }

        var tableSchema = _resolver.GetTableSchema(table);
        if (tableSchema is null)
        {
            _uniquenessLookupCache[table] = null;
            return null;
        }

        var uniqueColumnSets = new List<HashSet<string>>();

        if (tableSchema.PrimaryKey is not null)
        {
            uniqueColumnSets.Add(ToCaseInsensitiveSet(tableSchema.PrimaryKey.Columns));
        }

        if (tableSchema.UniqueConstraints is not null)
        {
            foreach (var uniqueConstraint in tableSchema.UniqueConstraints)
            {
                uniqueColumnSets.Add(ToCaseInsensitiveSet(uniqueConstraint.Columns));
            }
        }

        if (tableSchema.Indexes is not null)
        {
            foreach (var index in tableSchema.Indexes)
            {
                if (index.IsUnique)
                {
                    uniqueColumnSets.Add(ToCaseInsensitiveSet(index.Columns));
                }
            }
        }

        cached = uniqueColumnSets.Count == 0
            ? null
            : new TableUniquenessLookup(uniqueColumnSets);
        _uniquenessLookupCache[table] = cached;
        return cached;
    }

    private static HashSet<string> ToCaseInsensitiveSet(IReadOnlyList<string> columns)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var column in columns)
        {
            set.Add(column);
        }

        return set;
    }

    private sealed class TableUniquenessLookup(IReadOnlyList<HashSet<string>> uniqueColumnSets)
    {
        internal IReadOnlyList<HashSet<string>> UniqueColumnSets { get; } = uniqueColumnSets;
    }

    private sealed class ResolvedTableKeyComparer : IEqualityComparer<ResolvedTable>
    {
        public static ResolvedTableKeyComparer Instance { get; } = new();

        public bool Equals(ResolvedTable? x, ResolvedTable? y)
        {
            if (ReferenceEquals(x, y))
            {
                return true;
            }

            if (x is null || y is null)
            {
                return false;
            }

            return string.Equals(x.DatabaseName, y.DatabaseName, StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.SchemaName, y.SchemaName, StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.TableName, y.TableName, StringComparison.OrdinalIgnoreCase)
                && x.IsView == y.IsView;
        }

        public int GetHashCode(ResolvedTable obj)
        {
            var hash = new HashCode();
            hash.Add(obj.DatabaseName, StringComparer.OrdinalIgnoreCase);
            hash.Add(obj.SchemaName, StringComparer.OrdinalIgnoreCase);
            hash.Add(obj.TableName, StringComparer.OrdinalIgnoreCase);
            hash.Add(obj.IsView);
            return hash.ToHashCode();
        }
    }
}
