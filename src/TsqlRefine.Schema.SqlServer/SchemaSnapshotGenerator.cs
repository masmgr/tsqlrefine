using System.Collections.Frozen;
using System.Data;
using System.Globalization;
using System.Text;
using Microsoft.Data.SqlClient;
using TsqlRefine.Schema.Model;
using TsqlRefine.Schema.Snapshot;
using TsqlRefine.Schema.TypeSystem;

namespace TsqlRefine.Schema.SqlServer;

/// <summary>
/// Generates a <see cref="SchemaSnapshot"/> by querying SQL Server catalog views.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Maintainability", "CA1506", Justification = "Existing schema extraction orchestration; tracked as coupling baseline debt.")]
public static class SchemaSnapshotGenerator
{
    private const int MaxSchemaFilterParameters = 2000;

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

    internal static FrozenSet<string> BuildExcludeSet(SchemaSnapshotOptions options)
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

    internal static bool ShouldInclude(
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

    internal static SqlCommand CreateCatalogCommand(
        string query,
        SqlConnection connection,
        FrozenSet<string>? includeSchemas,
        FrozenSet<string> excludeSchemas)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(excludeSchemas);

        var parameterCount = (includeSchemas?.Count ?? 0) + excludeSchemas.Count;
        if (parameterCount > MaxSchemaFilterParameters)
        {
            return new SqlCommand(query.Replace(CatalogQueries.SchemaFilterMarker, string.Empty, StringComparison.Ordinal), connection);
        }

        var command = new SqlCommand(string.Empty, connection);
        var filter = new StringBuilder();
        AppendSchemaFilter(filter, command, includeSchemas, "include", "IN");
        AppendSchemaFilter(filter, command, excludeSchemas, "exclude", "NOT IN");
        command.CommandText = query.Replace(CatalogQueries.SchemaFilterMarker, filter.ToString(), StringComparison.Ordinal);
        return command;
    }

    private static void AppendSchemaFilter(
        StringBuilder filter,
        SqlCommand command,
        IEnumerable<string>? schemas,
        string parameterPrefix,
        string sqlOperator)
    {
        if (schemas is null)
        {
            return;
        }

        var schemaList = schemas.OrderBy(static schema => schema, StringComparer.OrdinalIgnoreCase).ToArray();
        if (schemaList.Length == 0)
        {
            return;
        }

        var parameterNames = new string[schemaList.Length];
        for (var i = 0; i < schemaList.Length; i++)
        {
            var parameterName = $"@{parameterPrefix}Schema{i}";
            parameterNames[i] = parameterName;
            command.Parameters.Add(new SqlParameter(parameterName, SqlDbType.NVarChar, 128)
            {
                Value = schemaList[i]
            });
        }

        filter.Append("AND s.name ")
            .Append(sqlOperator)
            .Append(" (")
            .AppendJoin(", ", parameterNames)
            .AppendLine(")");
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

        var databaseNameOrdinal = reader.GetOrdinal("DatabaseName");
        var serverNameOrdinal = reader.GetOrdinal("ServerName");
        var compatLevelOrdinal = reader.GetOrdinal("CompatLevel");

        return (
            reader.GetString(databaseNameOrdinal),
            reader.IsDBNull(serverNameOrdinal) ? string.Empty : reader.GetString(serverNameOrdinal),
            reader.GetByte(compatLevelOrdinal)
        );
    }

    internal sealed record TableEntry(string SchemaName, string ObjectName, bool IsView);

    private static async Task<List<TableEntry>> ReadTablesAndViewsAsync(
        SqlConnection connection,
        FrozenSet<string>? includeSchemas,
        FrozenSet<string> excludeSchemas,
        CancellationToken cancellationToken)
    {
        await using var cmd = CreateCatalogCommand(CatalogQueries.TablesAndViews, connection, includeSchemas, excludeSchemas);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        var result = new List<TableEntry>();
        var schemaNameOrdinal = reader.GetOrdinal("SchemaName");
        var objectNameOrdinal = reader.GetOrdinal("ObjectName");
        var typeDescOrdinal = reader.GetOrdinal("TypeDesc");
        while (await reader.ReadAsync(cancellationToken))
        {
            var schemaName = reader.GetString(schemaNameOrdinal);
            if (!ShouldInclude(schemaName, includeSchemas, excludeSchemas))
            {
                continue;
            }

            result.Add(new TableEntry(
                schemaName,
                reader.GetString(objectNameOrdinal),
                reader.GetString(typeDescOrdinal) == "VIEW"
            ));
        }

        return result;
    }

    internal sealed record ColumnEntry(
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
        await using var cmd = CreateCatalogCommand(CatalogQueries.Columns, connection, includeSchemas, excludeSchemas);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        var result = new List<ColumnEntry>();
        var schemaNameOrdinal = reader.GetOrdinal("SchemaName");
        var objectNameOrdinal = reader.GetOrdinal("ObjectName");
        var columnNameOrdinal = reader.GetOrdinal("ColumnName");
        var typeNameOrdinal = reader.GetOrdinal("TypeName");
        var maxLengthOrdinal = reader.GetOrdinal("MaxLength");
        var precisionOrdinal = reader.GetOrdinal("Precision");
        var scaleOrdinal = reader.GetOrdinal("Scale");
        var isNullableOrdinal = reader.GetOrdinal("IsNullable");
        var isIdentityOrdinal = reader.GetOrdinal("IsIdentity");
        var isComputedOrdinal = reader.GetOrdinal("IsComputed");
        var defaultExpressionOrdinal = reader.GetOrdinal("DefaultExpression");
        var collationOrdinal = reader.GetOrdinal("Collation");
        while (await reader.ReadAsync(cancellationToken))
        {
            var schemaName = reader.GetString(schemaNameOrdinal);
            if (!ShouldInclude(schemaName, includeSchemas, excludeSchemas))
            {
                continue;
            }

            result.Add(new ColumnEntry(
                schemaName,
                reader.GetString(objectNameOrdinal),
                reader.GetString(columnNameOrdinal),
                reader.GetString(typeNameOrdinal),
                reader.GetInt16(maxLengthOrdinal),
                reader.GetByte(precisionOrdinal),
                reader.GetByte(scaleOrdinal),
                reader.GetBoolean(isNullableOrdinal),
                reader.GetBoolean(isIdentityOrdinal),
                reader.GetBoolean(isComputedOrdinal),
                reader.IsDBNull(defaultExpressionOrdinal) ? null : reader.GetString(defaultExpressionOrdinal),
                reader.IsDBNull(collationOrdinal) ? null : reader.GetString(collationOrdinal)
            ));
        }

        return result;
    }

    internal sealed record PkEntry(string SchemaName, string TableName, bool IsClustered, string ColumnName);

    private static async Task<List<PkEntry>> ReadPrimaryKeysAsync(
        SqlConnection connection,
        FrozenSet<string>? includeSchemas,
        FrozenSet<string> excludeSchemas,
        CancellationToken cancellationToken)
    {
        await using var cmd = CreateCatalogCommand(CatalogQueries.PrimaryKeys, connection, includeSchemas, excludeSchemas);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        var result = new List<PkEntry>();
        var schemaNameOrdinal = reader.GetOrdinal("SchemaName");
        var tableNameOrdinal = reader.GetOrdinal("TableName");
        var indexTypeOrdinal = reader.GetOrdinal("IndexType");
        var columnNameOrdinal = reader.GetOrdinal("ColumnName");
        while (await reader.ReadAsync(cancellationToken))
        {
            var schemaName = reader.GetString(schemaNameOrdinal);
            if (!ShouldInclude(schemaName, includeSchemas, excludeSchemas))
            {
                continue;
            }

            result.Add(new PkEntry(
                schemaName,
                reader.GetString(tableNameOrdinal),
                reader.GetString(indexTypeOrdinal) == "CLUSTERED",
                reader.GetString(columnNameOrdinal)
            ));
        }

        return result;
    }

    internal sealed record UqEntry(string SchemaName, string TableName, string ConstraintName, string ColumnName);

    private static async Task<List<UqEntry>> ReadUniqueConstraintsAsync(
        SqlConnection connection,
        FrozenSet<string>? includeSchemas,
        FrozenSet<string> excludeSchemas,
        CancellationToken cancellationToken)
    {
        await using var cmd = CreateCatalogCommand(CatalogQueries.UniqueConstraints, connection, includeSchemas, excludeSchemas);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        var result = new List<UqEntry>();
        var schemaNameOrdinal = reader.GetOrdinal("SchemaName");
        var tableNameOrdinal = reader.GetOrdinal("TableName");
        var constraintNameOrdinal = reader.GetOrdinal("ConstraintName");
        var columnNameOrdinal = reader.GetOrdinal("ColumnName");
        while (await reader.ReadAsync(cancellationToken))
        {
            var schemaName = reader.GetString(schemaNameOrdinal);
            if (!ShouldInclude(schemaName, includeSchemas, excludeSchemas))
            {
                continue;
            }

            result.Add(new UqEntry(
                schemaName,
                reader.GetString(tableNameOrdinal),
                reader.GetString(constraintNameOrdinal),
                reader.GetString(columnNameOrdinal)
            ));
        }

        return result;
    }

    internal sealed record FkEntry(
        string SchemaName, string TableName, string ForeignKeyName,
        string SourceColumn, string TargetSchema, string TargetTable, string TargetColumn);

    private static async Task<List<FkEntry>> ReadForeignKeysAsync(
        SqlConnection connection,
        FrozenSet<string>? includeSchemas,
        FrozenSet<string> excludeSchemas,
        CancellationToken cancellationToken)
    {
        await using var cmd = CreateCatalogCommand(CatalogQueries.ForeignKeys, connection, includeSchemas, excludeSchemas);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        var result = new List<FkEntry>();
        var schemaNameOrdinal = reader.GetOrdinal("SchemaName");
        var tableNameOrdinal = reader.GetOrdinal("TableName");
        var foreignKeyNameOrdinal = reader.GetOrdinal("ForeignKeyName");
        var sourceColumnOrdinal = reader.GetOrdinal("SourceColumn");
        var targetSchemaOrdinal = reader.GetOrdinal("TargetSchema");
        var targetTableOrdinal = reader.GetOrdinal("TargetTable");
        var targetColumnOrdinal = reader.GetOrdinal("TargetColumn");
        while (await reader.ReadAsync(cancellationToken))
        {
            var schemaName = reader.GetString(schemaNameOrdinal);
            if (!ShouldInclude(schemaName, includeSchemas, excludeSchemas))
            {
                continue;
            }

            result.Add(new FkEntry(
                schemaName,
                reader.GetString(tableNameOrdinal),
                reader.GetString(foreignKeyNameOrdinal),
                reader.GetString(sourceColumnOrdinal),
                reader.GetString(targetSchemaOrdinal),
                reader.GetString(targetTableOrdinal),
                reader.GetString(targetColumnOrdinal)
            ));
        }

        return result;
    }

    internal sealed record IdxEntry(
        string SchemaName, string TableName, string IndexName,
        bool IsUnique, bool IsClustered, string ColumnName);

    private static async Task<List<IdxEntry>> ReadIndexesAsync(
        SqlConnection connection,
        FrozenSet<string>? includeSchemas,
        FrozenSet<string> excludeSchemas,
        CancellationToken cancellationToken)
    {
        await using var cmd = CreateCatalogCommand(CatalogQueries.Indexes, connection, includeSchemas, excludeSchemas);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        var result = new List<IdxEntry>();
        var schemaNameOrdinal = reader.GetOrdinal("SchemaName");
        var tableNameOrdinal = reader.GetOrdinal("TableName");
        var indexNameOrdinal = reader.GetOrdinal("IndexName");
        var isUniqueOrdinal = reader.GetOrdinal("IsUnique");
        var indexTypeOrdinal = reader.GetOrdinal("IndexType");
        var columnNameOrdinal = reader.GetOrdinal("ColumnName");
        while (await reader.ReadAsync(cancellationToken))
        {
            var schemaName = reader.GetString(schemaNameOrdinal);
            if (!ShouldInclude(schemaName, includeSchemas, excludeSchemas))
            {
                continue;
            }

            result.Add(new IdxEntry(
                schemaName,
                reader.GetString(tableNameOrdinal),
                reader.GetString(indexNameOrdinal),
                reader.GetBoolean(isUniqueOrdinal),
                reader.GetString(indexTypeOrdinal) == "CLUSTERED",
                reader.GetString(columnNameOrdinal)
            ));
        }

        return result;
    }

    internal sealed record TableBuildResult(TableSchema Schema, bool IsView);

    internal static List<TableBuildResult> BuildTableSchemas(
        List<TableEntry> tables,
        List<ColumnEntry> columns,
        List<PkEntry> primaryKeys,
        List<UqEntry> uniqueConstraints,
        List<FkEntry> foreignKeys,
        List<IdxEntry> indexes)
    {
        var columnsByTable = columns.GroupBy(c => (c.SchemaName, c.ObjectName), TableKeyComparer.Instance)
            .ToDictionary(g => g.Key, g => g.ToList(), TableKeyComparer.Instance);

        var pkByTable = primaryKeys.GroupBy(p => (p.SchemaName, p.TableName), TableKeyComparer.Instance)
            .ToDictionary(g => g.Key, g => g.ToList(), TableKeyComparer.Instance);

        var uqByTable = uniqueConstraints.GroupBy(u => (u.SchemaName, u.TableName), TableKeyComparer.Instance)
            .ToDictionary(g => g.Key, g => g.ToList(), TableKeyComparer.Instance);

        var fkByTable = foreignKeys.GroupBy(f => (f.SchemaName, f.TableName), TableKeyComparer.Instance)
            .ToDictionary(g => g.Key, g => g.ToList(), TableKeyComparer.Instance);

        var idxByTable = indexes.GroupBy(i => (i.SchemaName, i.TableName), TableKeyComparer.Instance)
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
