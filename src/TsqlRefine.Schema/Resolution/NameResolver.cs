using System.Diagnostics.CodeAnalysis;
using TsqlRefine.PluginSdk;
using TsqlRefine.Schema.Model;

namespace TsqlRefine.Schema.Resolution;

/// <summary>
/// Resolves table and column names against a <see cref="SchemaSnapshot"/>.
/// Supports 1-part, 2-part, and 3-part naming conventions with case-insensitive matching.
/// </summary>
internal sealed class NameResolver
{
    private readonly Dictionary<string, DatabaseLookup> _databases;
    private readonly string _defaultSchema;
    private readonly string? _defaultDatabaseName;

    internal NameResolver(SchemaSnapshot snapshot, string defaultSchema)
    {
        _defaultSchema = defaultSchema;
        _defaultDatabaseName = snapshot.Databases.Count > 0 ? snapshot.Databases[0].Name : null;
        _databases = new Dictionary<string, DatabaseLookup>(
            snapshot.Databases.Count, StringComparer.OrdinalIgnoreCase);

        foreach (var db in snapshot.Databases)
        {
            _databases[db.Name] = new DatabaseLookup(db);
        }
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
    internal List<(TableSchema SourceTable, ForeignKeyInfo ForeignKey)> GetReferencingForeignKeys(
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

    /// <summary>
    /// Internal lookup structure for a single database, providing fast case-insensitive
    /// table and view resolution by schema.table key.
    /// </summary>
    private sealed class DatabaseLookup
    {
        private readonly Dictionary<(string SchemaName, string TableName), TableLookup> _tables;
        private readonly Dictionary<(string SchemaName, string TableName), TableLookup> _views;
        private readonly Dictionary<(string SchemaName, string TableName), List<(TableSchema SourceTable, ForeignKeyInfo ForeignKey)>> _referencingFks;

        internal DatabaseLookup(DatabaseSchema db)
        {
            _tables = new Dictionary<(string SchemaName, string TableName), TableLookup>(
                db.Tables.Count, TableNameKeyComparer.Instance);
            _views = new Dictionary<(string SchemaName, string TableName), TableLookup>(
                db.Views.Count, TableNameKeyComparer.Instance);
            _referencingFks = new Dictionary<(string SchemaName, string TableName), List<(TableSchema, ForeignKeyInfo)>>(
                TableNameKeyComparer.Instance);

            foreach (var table in db.Tables)
            {
                _tables[(table.SchemaName, table.Name)] = new TableLookup(
                    db.Name, table, isView: false);
            }

            foreach (var view in db.Views)
            {
                _views[(view.SchemaName, view.Name)] = new TableLookup(
                    db.Name, view, isView: true);
            }

            // Build reverse FK index: target table key → list of (source table, FK)
            foreach (var table in db.Tables)
            {
                if (table.ForeignKeys is null)
                {
                    continue;
                }

                foreach (var fk in table.ForeignKeys)
                {
                    var targetKey = (fk.TargetSchema, fk.TargetTable);
                    if (!_referencingFks.TryGetValue(targetKey, out var list))
                    {
                        list = [];
                        _referencingFks[targetKey] = list;
                    }

                    list.Add((table, fk));
                }
            }
        }

        internal ResolvedTable? ResolveTable(string schemaName, string tableName)
        {
            var key = (schemaName, tableName);
            if (_tables.TryGetValue(key, out var tableLookup))
            {
                return tableLookup.ResolvedTable;
            }

            if (_views.TryGetValue(key, out var viewLookup))
            {
                return viewLookup.ResolvedTable;
            }

            return null;
        }

        internal TableSchema? FindTableSchema(string schemaName, string tableName, bool isView)
        {
            var lookup = isView ? _views : _tables;
            return lookup.TryGetValue((schemaName, tableName), out var tableLookup)
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

        internal List<(TableSchema SourceTable, ForeignKeyInfo ForeignKey)> GetReferencingForeignKeys(
            string schemaName, string tableName)
        {
            return _referencingFks.TryGetValue((schemaName, tableName), out var list) ? list : [];
        }

        private bool TryGetTableLookup(
            string schemaName,
            string tableName,
            bool isView,
            [NotNullWhen(true)] out TableLookup? tableLookup)
        {
            var lookup = isView ? _views : _tables;
            if (lookup.TryGetValue((schemaName, tableName), out var found))
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
        private readonly Dictionary<string, ResolvedColumn> _columns;
        private readonly SchemaColumnInfo[] _columnList;

        internal TableLookup(string databaseName, TableSchema tableSchema, bool isView)
        {
            TableSchema = tableSchema;
            ResolvedTable = new ResolvedTable(
                databaseName,
                tableSchema.SchemaName,
                tableSchema.Name,
                isView);

            _columns = new Dictionary<string, ResolvedColumn>(
                tableSchema.Columns.Count, StringComparer.OrdinalIgnoreCase);
            _columnList = new SchemaColumnInfo[tableSchema.Columns.Count];

            for (var i = 0; i < tableSchema.Columns.Count; i++)
            {
                var dto = tableSchema.Columns[i].ToDto();
                _columnList[i] = dto;
                // Keep the first entry when duplicate names exist in metadata.
                _columns.TryAdd(dto.Name, new ResolvedColumn(ResolvedTable, dto));
            }
        }

        internal TableSchema TableSchema { get; }

        internal ResolvedTable ResolvedTable { get; }

        internal IReadOnlyList<SchemaColumnInfo> Columns => _columnList;

        internal ResolvedColumn? ResolveColumn(string columnName) =>
            _columns.GetValueOrDefault(columnName);
    }

    private sealed class TableNameKeyComparer : IEqualityComparer<(string SchemaName, string TableName)>
    {
        public static TableNameKeyComparer Instance { get; } = new();

        public bool Equals(
            (string SchemaName, string TableName) x,
            (string SchemaName, string TableName) y) =>
            string.Equals(x.SchemaName, y.SchemaName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.TableName, y.TableName, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string SchemaName, string TableName) obj)
        {
            var hash = new HashCode();
            hash.Add(obj.SchemaName, StringComparer.OrdinalIgnoreCase);
            hash.Add(obj.TableName, StringComparer.OrdinalIgnoreCase);
            return hash.ToHashCode();
        }
    }
}
