using System.Collections.Frozen;
using System.Globalization;
using Microsoft.Data.SqlClient;
using TsqlRefine.Schema.Model;
using TsqlRefine.Schema.Snapshot;
using TsqlRefine.Schema.TypeSystem;

namespace TsqlRefine.Schema.SqlServer;

/// <summary>
/// Generates a <see cref="SchemaSnapshot"/> by querying SQL Server catalog views.
/// </summary>
public static class SchemaSnapshotGenerator
{
    private static readonly FrozenSet<string> DefaultExcludeSchemas =
        FrozenSet.ToFrozenSet(["sys", "INFORMATION_SCHEMA"], StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Generates a schema snapshot from a SQL Server database.
    /// </summary>
    /// <param name="connectionString">The SQL Server connection string.</param>
    /// <param name="options">Snapshot generation options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A complete schema snapshot.</returns>
    public static async Task<SchemaSnapshot> GenerateAsync(
        string connectionString,
        SchemaSnapshotOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        ArgumentNullException.ThrowIfNull(options);

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        var (dbName, serverName, dbCompatLevel) = await ReadDatabaseInfoAsync(connection, cancellationToken);
        var compatLevel = options.CompatLevel > 0 ? options.CompatLevel : dbCompatLevel;

        var excludeSchemas = BuildExcludeSet(options);
        var includeSchemas = options.IncludeSchemas is { Count: > 0 }
            ? options.IncludeSchemas.ToFrozenSet(StringComparer.OrdinalIgnoreCase)
            : null;

        var tables = await ReadTablesAndViewsAsync(connection, includeSchemas, excludeSchemas, cancellationToken);
        var columns = await ReadColumnsAsync(connection, includeSchemas, excludeSchemas, cancellationToken);
        var primaryKeys = await ReadPrimaryKeysAsync(connection, includeSchemas, excludeSchemas, cancellationToken);
        var uniqueConstraints = await ReadUniqueConstraintsAsync(connection, includeSchemas, excludeSchemas, cancellationToken);
        var foreignKeys = await ReadForeignKeysAsync(connection, includeSchemas, excludeSchemas, cancellationToken);
        var indexes = await ReadIndexesAsync(connection, includeSchemas, excludeSchemas, cancellationToken);

        var tableSchemas = BuildTableSchemas(tables, columns, primaryKeys, uniqueConstraints, foreignKeys, indexes);

        var tableList = tableSchemas.Where(t => !t.IsView).Select(t => t.Schema).ToArray();
        var viewList = tableSchemas.Where(t => t.IsView).Select(t => t.Schema).ToArray();

        var databases = new[]
        {
            new DatabaseSchema(dbName, tableList, viewList)
        };

        var contentHash = SchemaSnapshotSerializer.ComputeContentHash(databases);
        var metadata = new SnapshotMetadata(
            GeneratedAt: DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            ServerName: serverName,
            DatabaseName: dbName,
            CompatLevel: compatLevel,
            ContentHash: contentHash
        );

        return new SchemaSnapshot(metadata, databases);
    }

    private static FrozenSet<string> BuildExcludeSet(SchemaSnapshotOptions options)
    {
        if (options.ExcludeSchemas is not { Count: > 0 })
        {
            return DefaultExcludeSchemas;
        }

        var combined = new HashSet<string>(DefaultExcludeSchemas, StringComparer.OrdinalIgnoreCase);
        foreach (var s in options.ExcludeSchemas)
        {
            combined.Add(s);
        }

        return combined.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
    }

    private static bool ShouldInclude(
        string schemaName,
        FrozenSet<string>? includeSchemas,
        FrozenSet<string> excludeSchemas)
    {
        if (excludeSchemas.Contains(schemaName))
        {
            return false;
        }

        return includeSchemas is null || includeSchemas.Contains(schemaName);
    }

    private static async Task<(string DbName, string ServerName, int CompatLevel)> ReadDatabaseInfoAsync(
        SqlConnection connection, CancellationToken cancellationToken)
    {
        await using var cmd = new SqlCommand(CatalogQueries.DatabaseInfo, connection);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("Failed to read database information.");
        }

        return (
            reader.GetString(0),
            reader.GetString(1),
            reader.GetByte(2)
        );
    }

    private record TableEntry(string SchemaName, string ObjectName, bool IsView);

    private static async Task<List<TableEntry>> ReadTablesAndViewsAsync(
        SqlConnection connection,
        FrozenSet<string>? includeSchemas,
        FrozenSet<string> excludeSchemas,
        CancellationToken cancellationToken)
    {
        await using var cmd = new SqlCommand(CatalogQueries.TablesAndViews, connection);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        var result = new List<TableEntry>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var schemaName = reader.GetString(0);
            if (!ShouldInclude(schemaName, includeSchemas, excludeSchemas))
            {
                continue;
            }

            result.Add(new TableEntry(
                schemaName,
                reader.GetString(1),
                reader.GetString(2) == "VIEW"
            ));
        }

        return result;
    }

    private record ColumnEntry(
        string SchemaName, string ObjectName, string ColumnName, string TypeName,
        short MaxLength, byte Precision, byte Scale,
        bool IsNullable, bool IsIdentity, bool IsComputed,
        string? DefaultExpression, string? Collation);

    private static async Task<List<ColumnEntry>> ReadColumnsAsync(
        SqlConnection connection,
        FrozenSet<string>? includeSchemas,
        FrozenSet<string> excludeSchemas,
        CancellationToken cancellationToken)
    {
        await using var cmd = new SqlCommand(CatalogQueries.Columns, connection);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        var result = new List<ColumnEntry>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var schemaName = reader.GetString(0);
            if (!ShouldInclude(schemaName, includeSchemas, excludeSchemas))
            {
                continue;
            }

            result.Add(new ColumnEntry(
                schemaName,
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetInt16(4),
                reader.GetByte(5),
                reader.GetByte(6),
                reader.GetBoolean(7),
                reader.GetBoolean(8),
                reader.GetBoolean(9),
                reader.IsDBNull(10) ? null : reader.GetString(10),
                reader.IsDBNull(11) ? null : reader.GetString(11)
            ));
        }

        return result;
    }

    private record PkEntry(string SchemaName, string TableName, bool IsClustered, string ColumnName);

    private static async Task<List<PkEntry>> ReadPrimaryKeysAsync(
        SqlConnection connection,
        FrozenSet<string>? includeSchemas,
        FrozenSet<string> excludeSchemas,
        CancellationToken cancellationToken)
    {
        await using var cmd = new SqlCommand(CatalogQueries.PrimaryKeys, connection);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        var result = new List<PkEntry>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var schemaName = reader.GetString(0);
            if (!ShouldInclude(schemaName, includeSchemas, excludeSchemas))
            {
                continue;
            }

            result.Add(new PkEntry(
                schemaName,
                reader.GetString(1),
                reader.GetString(3) == "CLUSTERED",
                reader.GetString(5)
            ));
        }

        return result;
    }

    private record UqEntry(string SchemaName, string TableName, string ConstraintName, string ColumnName);

    private static async Task<List<UqEntry>> ReadUniqueConstraintsAsync(
        SqlConnection connection,
        FrozenSet<string>? includeSchemas,
        FrozenSet<string> excludeSchemas,
        CancellationToken cancellationToken)
    {
        await using var cmd = new SqlCommand(CatalogQueries.UniqueConstraints, connection);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        var result = new List<UqEntry>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var schemaName = reader.GetString(0);
            if (!ShouldInclude(schemaName, includeSchemas, excludeSchemas))
            {
                continue;
            }

            result.Add(new UqEntry(
                schemaName,
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(4)
            ));
        }

        return result;
    }

    private record FkEntry(
        string SchemaName, string TableName, string ForeignKeyName,
        string SourceColumn, string TargetSchema, string TargetTable, string TargetColumn);

    private static async Task<List<FkEntry>> ReadForeignKeysAsync(
        SqlConnection connection,
        FrozenSet<string>? includeSchemas,
        FrozenSet<string> excludeSchemas,
        CancellationToken cancellationToken)
    {
        await using var cmd = new SqlCommand(CatalogQueries.ForeignKeys, connection);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        var result = new List<FkEntry>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var schemaName = reader.GetString(0);
            if (!ShouldInclude(schemaName, includeSchemas, excludeSchemas))
            {
                continue;
            }

            result.Add(new FkEntry(
                schemaName,
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6),
                reader.GetString(7)
            ));
        }

        return result;
    }

    private record IdxEntry(
        string SchemaName, string TableName, string IndexName,
        bool IsUnique, bool IsClustered, string ColumnName);

    private static async Task<List<IdxEntry>> ReadIndexesAsync(
        SqlConnection connection,
        FrozenSet<string>? includeSchemas,
        FrozenSet<string> excludeSchemas,
        CancellationToken cancellationToken)
    {
        await using var cmd = new SqlCommand(CatalogQueries.Indexes, connection);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        var result = new List<IdxEntry>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var schemaName = reader.GetString(0);
            if (!ShouldInclude(schemaName, includeSchemas, excludeSchemas))
            {
                continue;
            }

            result.Add(new IdxEntry(
                schemaName,
                reader.GetString(1),
                reader.GetString(2),
                reader.GetBoolean(3),
                reader.GetString(4) == "CLUSTERED",
                reader.GetString(6)
            ));
        }

        return result;
    }

    private record TableBuildResult(TableSchema Schema, bool IsView);

    private static List<TableBuildResult> BuildTableSchemas(
        List<TableEntry> tables,
        List<ColumnEntry> columns,
        List<PkEntry> primaryKeys,
        List<UqEntry> uniqueConstraints,
        List<FkEntry> foreignKeys,
        List<IdxEntry> indexes)
    {
        var columnsByTable = columns.GroupBy(c => (c.SchemaName, c.ObjectName))
            .ToDictionary(g => g.Key, g => g.ToList(), TableKeyComparer.Instance);

        var pkByTable = primaryKeys.GroupBy(p => (p.SchemaName, p.TableName))
            .ToDictionary(g => g.Key, g => g.ToList(), TableKeyComparer.Instance);

        var uqByTable = uniqueConstraints.GroupBy(u => (u.SchemaName, u.TableName))
            .ToDictionary(g => g.Key, g => g.ToList(), TableKeyComparer.Instance);

        var fkByTable = foreignKeys.GroupBy(f => (f.SchemaName, f.TableName))
            .ToDictionary(g => g.Key, g => g.ToList(), TableKeyComparer.Instance);

        var idxByTable = indexes.GroupBy(i => (i.SchemaName, i.TableName))
            .ToDictionary(g => g.Key, g => g.ToList(), TableKeyComparer.Instance);

        var result = new List<TableBuildResult>(tables.Count);
        foreach (var table in tables)
        {
            var key = (table.SchemaName, table.ObjectName);

            var cols = columnsByTable.TryGetValue(key, out var colList)
                ? colList.Select(c => new ColumnSchema(
                    c.ColumnName,
                    new SqlTypeInfo(
                        c.TypeName,
                        TypeCategoryMapper.FromTypeName(c.TypeName),
                        c.MaxLength == 0 ? null : (int)c.MaxLength,
                        c.Precision == 0 ? null : (int)c.Precision,
                        c.Scale == 0 && c.Precision == 0 ? null : (int)c.Scale
                    ),
                    c.IsNullable,
                    IsIdentity: c.IsIdentity,
                    IsComputed: c.IsComputed,
                    DefaultExpression: c.DefaultExpression,
                    Collation: c.Collation
                )).ToArray()
                : Array.Empty<ColumnSchema>();

            PrimaryKeyInfo? pk = null;
            if (pkByTable.TryGetValue(key, out var pkList))
            {
                var pkColumns = pkList.Select(p => p.ColumnName).ToArray();
                pk = new PrimaryKeyInfo(pkColumns, pkList[0].IsClustered);
            }

            var uqs = uqByTable.TryGetValue(key, out var uqList)
                ? uqList.GroupBy(u => u.ConstraintName, StringComparer.OrdinalIgnoreCase)
                    .Select(g => new UniqueConstraintInfo(g.Key, g.Select(u => u.ColumnName).ToArray()))
                    .ToArray()
                : null;

            var fks = fkByTable.TryGetValue(key, out var fkList)
                ? fkList.GroupBy(f => f.ForeignKeyName, StringComparer.OrdinalIgnoreCase)
                    .Select(g =>
                    {
                        var first = g.First();
                        return new ForeignKeyInfo(
                            g.Key,
                            g.Select(f => f.SourceColumn).ToArray(),
                            first.TargetSchema,
                            first.TargetTable,
                            g.Select(f => f.TargetColumn).ToArray()
                        );
                    })
                    .ToArray()
                : null;

            var idxs = idxByTable.TryGetValue(key, out var idxList)
                ? idxList.GroupBy(i => i.IndexName, StringComparer.OrdinalIgnoreCase)
                    .Select(g =>
                    {
                        var first = g.First();
                        return new IndexInfo(
                            g.Key,
                            g.Select(i => i.ColumnName).ToArray(),
                            first.IsUnique,
                            first.IsClustered
                        );
                    })
                    .ToArray()
                : null;

            var schema = new TableSchema(
                table.SchemaName,
                table.ObjectName,
                cols,
                pk,
                uqs,
                fks,
                idxs
            );

            result.Add(new TableBuildResult(schema, table.IsView));
        }

        return result;
    }

    private sealed class TableKeyComparer : IEqualityComparer<(string, string)>
    {
        public static readonly TableKeyComparer Instance = new();

        public bool Equals((string, string) x, (string, string) y) =>
            StringComparer.OrdinalIgnoreCase.Equals(x.Item1, y.Item1) &&
            StringComparer.OrdinalIgnoreCase.Equals(x.Item2, y.Item2);

        public int GetHashCode((string, string) obj) =>
            HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Item1),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Item2));
    }
}
