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
        _databases = new Dictionary<string, DatabaseLookup>(StringComparer.OrdinalIgnoreCase);

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

        return dbLookup.ResolveTable(dbName, schemaName, name);
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

        var tableSchema = dbLookup.FindTableSchema(table.SchemaName, table.TableName, table.IsView);
        if (tableSchema is null)
        {
            return null;
        }

        var column = tableSchema.Columns
            .FirstOrDefault(c => string.Equals(c.Name, columnName, StringComparison.OrdinalIgnoreCase));

        return column is null ? null : new ResolvedColumn(table, column.ToDto());
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

        var tableSchema = dbLookup.FindTableSchema(table.SchemaName, table.TableName, table.IsView);
        if (tableSchema is null)
        {
            return [];
        }

        return tableSchema.Columns.Select(c => c.ToDto()).ToArray();
    }

    /// <summary>
    /// Internal lookup structure for a single database, providing fast case-insensitive
    /// table and view resolution by schema.table key.
    /// </summary>
    private sealed class DatabaseLookup
    {
        private readonly Dictionary<string, TableSchema> _tables;
        private readonly Dictionary<string, TableSchema> _views;
        private readonly Dictionary<string, List<(TableSchema SourceTable, ForeignKeyInfo ForeignKey)>> _referencingFks;

        internal DatabaseLookup(DatabaseSchema db)
        {
            _tables = new Dictionary<string, TableSchema>(StringComparer.OrdinalIgnoreCase);
            _views = new Dictionary<string, TableSchema>(StringComparer.OrdinalIgnoreCase);
            _referencingFks = new Dictionary<string, List<(TableSchema, ForeignKeyInfo)>>(StringComparer.OrdinalIgnoreCase);

            foreach (var table in db.Tables)
            {
                _tables[$"{table.SchemaName}.{table.Name}"] = table;
            }

            foreach (var view in db.Views)
            {
                _views[$"{view.SchemaName}.{view.Name}"] = view;
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
                    var targetKey = $"{fk.TargetSchema}.{fk.TargetTable}";
                    if (!_referencingFks.TryGetValue(targetKey, out var list))
                    {
                        list = [];
                        _referencingFks[targetKey] = list;
                    }

                    list.Add((table, fk));
                }
            }
        }

        internal ResolvedTable? ResolveTable(string databaseName, string schemaName, string tableName)
        {
            var key = $"{schemaName}.{tableName}";
            if (_tables.TryGetValue(key, out _))
            {
                return new ResolvedTable(databaseName, schemaName, tableName, IsView: false);
            }

            if (_views.TryGetValue(key, out _))
            {
                return new ResolvedTable(databaseName, schemaName, tableName, IsView: true);
            }

            return null;
        }

        internal TableSchema? FindTableSchema(string schemaName, string tableName, bool isView)
        {
            var key = $"{schemaName}.{tableName}";
            var lookup = isView ? _views : _tables;
            return lookup.GetValueOrDefault(key);
        }

        internal List<(TableSchema SourceTable, ForeignKeyInfo ForeignKey)> GetReferencingForeignKeys(
            string schemaName, string tableName)
        {
            var key = $"{schemaName}.{tableName}";
            return _referencingFks.TryGetValue(key, out var list) ? list : [];
        }
    }
}
