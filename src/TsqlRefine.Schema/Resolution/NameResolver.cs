using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using TsqlRefine.PluginSdk;
using TsqlRefine.Schema.Model;

namespace TsqlRefine.Schema.Resolution;

/// <summary>
/// Resolves table and column names against a <see cref="SchemaSnapshot"/>.
/// Supports 1-part, 2-part, and 3-part naming conventions using snapshot collation metadata.
/// </summary>
internal sealed class NameResolver
{
    private readonly FrozenDictionary<string, DatabaseLookup> _databases;
    private readonly string _defaultSchema;
    private readonly string? _defaultDatabaseName;

    internal NameResolver(SchemaSnapshot snapshot, string defaultSchema)
    {
        _defaultSchema = defaultSchema;
        _defaultDatabaseName = snapshot.Databases.Count > 0 ? snapshot.Databases[0].Name : null;
        var identifierComparer = SqlIdentifierComparer.Create(snapshot.Metadata);
        _databases = snapshot.Databases
            .ToFrozenDictionary(
                db => db.Name,
                db => new DatabaseLookup(db, identifierComparer),
                StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets the internal <see cref="TableSchema"/> for a resolved table.
    /// </summary>
    internal TableSchema? GetTableSchema(ResolvedTable table)
    {
        if (!_databases.TryGetValue(table.DatabaseName, out var dbLookup))
        {
            return null;
        }

        return dbLookup.FindTableSchema(table.SchemaName, table.TableName, table.IsView);
    }

    /// <summary>
    /// Gets all foreign keys from other tables that reference the specified table.
    /// </summary>
    internal IReadOnlyList<(TableSchema SourceTable, ForeignKeyInfo ForeignKey)> GetReferencingForeignKeys(
        ResolvedTable table)
    {
        if (!_databases.TryGetValue(table.DatabaseName, out var dbLookup))
        {
            return [];
        }

        return dbLookup.GetReferencingForeignKeys(table.SchemaName, table.TableName);
    }

    /// <summary>
    /// Resolves a table or view by 1, 2, or 3-part name.
    /// </summary>
    internal ResolvedTable? ResolveTable(string? database, string? schema, string name)
    {
        // Determine which database to search
        var dbName = database ?? _defaultDatabaseName;
        if (dbName is null || !_databases.TryGetValue(dbName, out var dbLookup))
        {
            return null;
        }

        // Determine which schema to search
        var schemaName = schema ?? _defaultSchema;

        return dbLookup.ResolveTable(schemaName, name);
    }

    /// <summary>
    /// Resolves a column within a previously resolved table.
    /// </summary>
    internal ResolvedColumn? ResolveColumn(ResolvedTable table, string columnName)
    {
        if (!_databases.TryGetValue(table.DatabaseName, out var dbLookup))
        {
            return null;
        }

        return dbLookup.ResolveColumn(table.SchemaName, table.TableName, table.IsView, columnName);
    }

    /// <summary>
    /// Gets all columns for a resolved table.
    /// </summary>
    internal IReadOnlyList<SchemaColumnInfo> GetColumns(ResolvedTable table)
    {
        if (!_databases.TryGetValue(table.DatabaseName, out var dbLookup))
        {
            return [];
        }

        return dbLookup.GetColumns(table.SchemaName, table.TableName, table.IsView);
    }

    internal bool IsColumnLookupCreated(ResolvedTable table) =>
        _databases.TryGetValue(table.DatabaseName, out var dbLookup) &&
        dbLookup.IsColumnLookupCreated(table.SchemaName, table.TableName, table.IsView);

    /// <summary>
    /// Internal lookup structure for a single database, providing collation-aware
    /// table and view resolution by schema.table key.
    /// </summary>
    private sealed class DatabaseLookup
    {
        private readonly FrozenDictionary<(string SchemaName, string TableName), TableLookup> _objects;
        private readonly Lazy<FrozenDictionary<(string SchemaName, string TableName), IReadOnlyList<(TableSchema SourceTable, ForeignKeyInfo ForeignKey)>>> _referencingFks;

        internal DatabaseLookup(DatabaseSchema db, IEqualityComparer<string> identifierComparer)
        {
            var tableNameComparer = new TableNameKeyComparer(identifierComparer);
            var objects = new Dictionary<(string SchemaName, string TableName), TableLookup>(
                db.Tables.Count + db.Views.Count,
                tableNameComparer);
            foreach (var table in db.Tables)
            {
                objects.Add(
                    (table.SchemaName, table.Name),
                    new TableLookup(db.Name, table, isView: false, identifierComparer));
            }

            foreach (var view in db.Views)
            {
                objects.Add(
                    (view.SchemaName, view.Name),
                    new TableLookup(db.Name, view, isView: true, identifierComparer));
            }

            _objects = objects.ToFrozenDictionary(tableNameComparer);
            _referencingFks = new Lazy<FrozenDictionary<(string SchemaName, string TableName), IReadOnlyList<(TableSchema SourceTable, ForeignKeyInfo ForeignKey)>>>(
                () => BuildReferencingForeignKeys(db.Tables, tableNameComparer),
                LazyThreadSafetyMode.ExecutionAndPublication);
        }

        private static FrozenDictionary<(string SchemaName, string TableName), IReadOnlyList<(TableSchema SourceTable, ForeignKeyInfo ForeignKey)>> BuildReferencingForeignKeys(
            IReadOnlyList<TableSchema> tables,
            TableNameKeyComparer tableNameComparer)
        {
            var referencingFksBuilder = new Dictionary<(string, string), List<(TableSchema, ForeignKeyInfo)>>(
                tableNameComparer);
            foreach (var table in tables)
            {
                if (table.ForeignKeys is null)
                {
                    continue;
                }

                foreach (var fk in table.ForeignKeys)
                {
                    var targetKey = (fk.TargetSchema, fk.TargetTable);
                    if (!referencingFksBuilder.TryGetValue(targetKey, out var list))
                    {
                        list = [];
                        referencingFksBuilder[targetKey] = list;
                    }

                    list.Add((table, fk));
                }
            }

            return referencingFksBuilder
                .ToFrozenDictionary(
                    kvp => kvp.Key,
                    kvp => (IReadOnlyList<(TableSchema SourceTable, ForeignKeyInfo ForeignKey)>)kvp.Value,
                    tableNameComparer);
        }

        internal ResolvedTable? ResolveTable(string schemaName, string tableName)
        {
            return _objects.TryGetValue((schemaName, tableName), out var lookup)
                ? lookup.ResolvedTable
                : null;
        }

        internal TableSchema? FindTableSchema(string schemaName, string tableName, bool isView)
        {
            return TryGetTableLookup(schemaName, tableName, isView, out var tableLookup)
                ? tableLookup.TableSchema
                : null;
        }

        internal ResolvedColumn? ResolveColumn(
            string schemaName, string tableName, bool isView, string columnName)
        {
            if (!TryGetTableLookup(schemaName, tableName, isView, out var tableLookup))
            {
                return null;
            }

            return tableLookup.ResolveColumn(columnName);
        }

        internal IReadOnlyList<SchemaColumnInfo> GetColumns(
            string schemaName, string tableName, bool isView)
        {
            if (!TryGetTableLookup(schemaName, tableName, isView, out var tableLookup))
            {
                return [];
            }

            return tableLookup.Columns;
        }

        internal IReadOnlyList<(TableSchema SourceTable, ForeignKeyInfo ForeignKey)> GetReferencingForeignKeys(
            string schemaName, string tableName)
        {
            return _referencingFks.Value.TryGetValue((schemaName, tableName), out var list) ? list : [];
        }

        internal bool IsColumnLookupCreated(string schemaName, string tableName, bool isView)
        {
            return TryGetTableLookup(schemaName, tableName, isView, out var tableLookup) &&
                tableLookup.IsColumnLookupCreated;
        }

        private bool TryGetTableLookup(
            string schemaName,
            string tableName,
            bool isView,
            [NotNullWhen(true)] out TableLookup? tableLookup)
        {
            if (_objects.TryGetValue((schemaName, tableName), out var found) &&
                found.ResolvedTable.IsView == isView)
            {
                tableLookup = found;
                return true;
            }

            tableLookup = null;
            return false;
        }
    }

    private sealed class TableLookup
    {
        private readonly Lazy<ColumnLookup> _columns;

        internal TableLookup(
            string databaseName,
            TableSchema tableSchema,
            bool isView,
            IEqualityComparer<string> identifierComparer)
        {
            TableSchema = tableSchema;
            ResolvedTable = new ResolvedTable(
                databaseName,
                tableSchema.SchemaName,
                tableSchema.Name,
                isView);

            _columns = new Lazy<ColumnLookup>(
                () => new ColumnLookup(ResolvedTable, tableSchema.Columns, identifierComparer),
                LazyThreadSafetyMode.ExecutionAndPublication);
        }

        internal TableSchema TableSchema { get; }

        internal ResolvedTable ResolvedTable { get; }

        internal IReadOnlyList<SchemaColumnInfo> Columns => _columns.Value.ColumnList;

        internal bool IsColumnLookupCreated => _columns.IsValueCreated;

        internal ResolvedColumn? ResolveColumn(string columnName) =>
            _columns.Value.Columns.GetValueOrDefault(columnName);
    }

    private sealed class ColumnLookup
    {
        internal ColumnLookup(
            ResolvedTable resolvedTable,
            IReadOnlyList<ColumnSchema> columns,
            IEqualityComparer<string> identifierComparer)
        {
            ColumnList = new SchemaColumnInfo[columns.Count];
            var columnsBuilder = new Dictionary<string, ResolvedColumn>(columns.Count, identifierComparer);

            for (var i = 0; i < columns.Count; i++)
            {
                var dto = columns[i].ToDto();
                ColumnList[i] = dto;
                columnsBuilder.TryAdd(dto.Name, new ResolvedColumn(resolvedTable, dto));
            }

            Columns = columnsBuilder.ToFrozenDictionary(identifierComparer);
        }

        internal FrozenDictionary<string, ResolvedColumn> Columns { get; }

        internal SchemaColumnInfo[] ColumnList { get; }
    }

    private sealed class TableNameKeyComparer(IEqualityComparer<string> identifierComparer)
        : IEqualityComparer<(string SchemaName, string TableName)>
    {
        public bool Equals(
            (string SchemaName, string TableName) x,
            (string SchemaName, string TableName) y) =>
            identifierComparer.Equals(x.SchemaName, y.SchemaName) &&
            identifierComparer.Equals(x.TableName, y.TableName);

        public int GetHashCode((string SchemaName, string TableName) obj)
        {
            var hash = new HashCode();
            hash.Add(obj.SchemaName, identifierComparer);
            hash.Add(obj.TableName, identifierComparer);
            return hash.ToHashCode();
        }
    }
}
