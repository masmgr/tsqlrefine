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

        var (dbName, serverName, dbCompatLevel, databaseCollation, collationLcid, collationComparisonStyle) =
            await ReadDatabaseInfoAsync(connection, cancellationToken);
        var compatLevel = options.CompatLevel > 0 ? options.CompatLevel : dbCompatLevel;

        var excludeSchemas = BuildExcludeSet(options);
        var includeSchemas = options.IncludeSchemas is { Count: > 0 }
            ? options.IncludeSchemas.ToFrozenSet(StringComparer.OrdinalIgnoreCase)
            : null;

        var tables = await ReadTablesAndViewsAsync(connection, includeSchemas, excludeSchemas, cancellationToken);
        var accumulators = tables.ToDictionary(
            t => (t.SchemaName, t.ObjectName),
            t => new TableAccumulator(t),
            TableKeyComparer.Instance);
        await ReadColumnsAsync(connection, includeSchemas, excludeSchemas, accumulators, cancellationToken);
        await ReadPrimaryKeysAsync(connection, includeSchemas, excludeSchemas, accumulators, cancellationToken);
        await ReadUniqueConstraintsAsync(connection, includeSchemas, excludeSchemas, accumulators, cancellationToken);
        await ReadForeignKeysAsync(connection, includeSchemas, excludeSchemas, accumulators, cancellationToken);
        await ReadIndexesAsync(connection, includeSchemas, excludeSchemas, accumulators, cancellationToken);

        var tableSchemas = tables.Select(t => accumulators[(t.SchemaName, t.ObjectName)].Build()).ToArray();

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
        )
        {
            DatabaseCollation = databaseCollation,
            CollationLcid = collationLcid,
            CollationComparisonStyle = collationComparisonStyle
        };

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

    private static async Task<(
        string DbName,
        string ServerName,
        int CompatLevel,
        string? DatabaseCollation,
        int? CollationLcid,
        int? CollationComparisonStyle)> ReadDatabaseInfoAsync(
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
        var databaseCollationOrdinal = reader.GetOrdinal("DatabaseCollation");
        var collationLcidOrdinal = reader.GetOrdinal("CollationLcid");
        var collationComparisonStyleOrdinal = reader.GetOrdinal("CollationComparisonStyle");

        return (
            reader.GetString(databaseNameOrdinal),
            reader.IsDBNull(serverNameOrdinal) ? string.Empty : reader.GetString(serverNameOrdinal),
            reader.GetByte(compatLevelOrdinal),
            reader.IsDBNull(databaseCollationOrdinal) ? null : reader.GetString(databaseCollationOrdinal),
            reader.IsDBNull(collationLcidOrdinal) ? null : reader.GetInt32(collationLcidOrdinal),
            reader.IsDBNull(collationComparisonStyleOrdinal) ? null : reader.GetInt32(collationComparisonStyleOrdinal)
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

    private static async Task ReadColumnsAsync(
        SqlConnection connection,
        FrozenSet<string>? includeSchemas,
        FrozenSet<string> excludeSchemas,
        Dictionary<(string SchemaName, string TableName), TableAccumulator> accumulators,
        CancellationToken cancellationToken)
    {
        await using var cmd = CreateCatalogCommand(CatalogQueries.Columns, connection, includeSchemas, excludeSchemas);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

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

            var objectName = reader.GetString(objectNameOrdinal);
            if (!accumulators.TryGetValue((schemaName, objectName), out var accumulator))
            {
                continue;
            }

            accumulator.Columns.Add(new ColumnSchema(
                reader.GetString(columnNameOrdinal),
                CreateSqlTypeInfo(
                    reader.GetString(typeNameOrdinal),
                    reader.GetInt16(maxLengthOrdinal),
                    reader.GetByte(precisionOrdinal),
                    reader.GetByte(scaleOrdinal)),
                reader.GetBoolean(isNullableOrdinal),
                IsIdentity: reader.GetBoolean(isIdentityOrdinal),
                IsComputed: reader.GetBoolean(isComputedOrdinal),
                DefaultExpression: reader.IsDBNull(defaultExpressionOrdinal) ? null : reader.GetString(defaultExpressionOrdinal),
                Collation: reader.IsDBNull(collationOrdinal) ? null : reader.GetString(collationOrdinal)
            ));
        }
    }

    internal sealed record PkEntry(string SchemaName, string TableName, bool IsClustered, string ColumnName);

    private static async Task ReadPrimaryKeysAsync(
        SqlConnection connection,
        FrozenSet<string>? includeSchemas,
        FrozenSet<string> excludeSchemas,
        Dictionary<(string SchemaName, string TableName), TableAccumulator> accumulators,
        CancellationToken cancellationToken)
    {
        await using var cmd = CreateCatalogCommand(CatalogQueries.PrimaryKeys, connection, includeSchemas, excludeSchemas);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

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

            var tableName = reader.GetString(tableNameOrdinal);
            if (accumulators.TryGetValue((schemaName, tableName), out var accumulator))
            {
                accumulator.PrimaryKeyIsClustered ??= reader.GetString(indexTypeOrdinal) == "CLUSTERED";
                accumulator.PrimaryKeyColumns.Add(reader.GetString(columnNameOrdinal));
            }
        }
    }

    internal sealed record UqEntry(string SchemaName, string TableName, string ConstraintName, string ColumnName);

    private static async Task ReadUniqueConstraintsAsync(
        SqlConnection connection,
        FrozenSet<string>? includeSchemas,
        FrozenSet<string> excludeSchemas,
        Dictionary<(string SchemaName, string TableName), TableAccumulator> accumulators,
        CancellationToken cancellationToken)
    {
        await using var cmd = CreateCatalogCommand(CatalogQueries.UniqueConstraints, connection, includeSchemas, excludeSchemas);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

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

            var tableName = reader.GetString(tableNameOrdinal);
            if (accumulators.TryGetValue((schemaName, tableName), out var accumulator))
            {
                var constraintName = reader.GetString(constraintNameOrdinal);
                if (!accumulator.UniqueConstraints.TryGetValue(constraintName, out var columns))
                {
                    columns = [];
                    accumulator.UniqueConstraints.Add(constraintName, columns);
                }

                columns.Add(reader.GetString(columnNameOrdinal));
            }
        }
    }

    internal sealed record FkEntry(
        string SchemaName, string TableName, string ForeignKeyName,
        string SourceColumn, string TargetSchema, string TargetTable, string TargetColumn);

    private static async Task ReadForeignKeysAsync(
        SqlConnection connection,
        FrozenSet<string>? includeSchemas,
        FrozenSet<string> excludeSchemas,
        Dictionary<(string SchemaName, string TableName), TableAccumulator> accumulators,
        CancellationToken cancellationToken)
    {
        await using var cmd = CreateCatalogCommand(CatalogQueries.ForeignKeys, connection, includeSchemas, excludeSchemas);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

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

            var tableName = reader.GetString(tableNameOrdinal);
            if (accumulators.TryGetValue((schemaName, tableName), out var accumulator))
            {
                var foreignKeyName = reader.GetString(foreignKeyNameOrdinal);
                if (!accumulator.ForeignKeys.TryGetValue(foreignKeyName, out var foreignKey))
                {
                    foreignKey = new ForeignKeyAccumulator(
                        reader.GetString(targetSchemaOrdinal),
                        reader.GetString(targetTableOrdinal));
                    accumulator.ForeignKeys.Add(foreignKeyName, foreignKey);
                }

                foreignKey.SourceColumns.Add(reader.GetString(sourceColumnOrdinal));
                foreignKey.TargetColumns.Add(reader.GetString(targetColumnOrdinal));
            }
        }
    }

    internal sealed record IdxEntry(
        string SchemaName, string TableName, string IndexName,
        bool IsUnique, bool IsClustered, string ColumnName);

    private static async Task ReadIndexesAsync(
        SqlConnection connection,
        FrozenSet<string>? includeSchemas,
        FrozenSet<string> excludeSchemas,
        Dictionary<(string SchemaName, string TableName), TableAccumulator> accumulators,
        CancellationToken cancellationToken)
    {
        await using var cmd = CreateCatalogCommand(CatalogQueries.Indexes, connection, includeSchemas, excludeSchemas);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

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

            var tableName = reader.GetString(tableNameOrdinal);
            if (accumulators.TryGetValue((schemaName, tableName), out var accumulator))
            {
                var indexName = reader.GetString(indexNameOrdinal);
                if (!accumulator.Indexes.TryGetValue(indexName, out var index))
                {
                    index = new IndexAccumulator(
                        reader.GetBoolean(isUniqueOrdinal),
                        reader.GetString(indexTypeOrdinal) == "CLUSTERED");
                    accumulator.Indexes.Add(indexName, index);
                }

                index.Columns.Add(reader.GetString(columnNameOrdinal));
            }
        }
    }

    private static SqlTypeInfo CreateSqlTypeInfo(
        string typeName,
        short maxLength,
        byte precision,
        byte scale) =>
        new(
            typeName,
            TypeCategoryMapper.FromTypeName(typeName),
            maxLength == 0 ? null : (int)maxLength,
            precision == 0 ? null : (int)precision,
            scale == 0 && precision == 0 ? null : (int)scale);

    private sealed class TableAccumulator(TableEntry table)
    {
        internal List<ColumnSchema> Columns { get; } = [];

        internal List<string> PrimaryKeyColumns { get; } = [];

        internal bool? PrimaryKeyIsClustered { get; set; }

        internal Dictionary<string, List<string>> UniqueConstraints { get; } =
            new(StringComparer.OrdinalIgnoreCase);

        internal Dictionary<string, ForeignKeyAccumulator> ForeignKeys { get; } =
            new(StringComparer.OrdinalIgnoreCase);

        internal Dictionary<string, IndexAccumulator> Indexes { get; } =
            new(StringComparer.OrdinalIgnoreCase);

        internal TableBuildResult Build()
        {
            var primaryKey = PrimaryKeyColumns.Count == 0
                ? null
                : new PrimaryKeyInfo(PrimaryKeyColumns.ToArray(), PrimaryKeyIsClustered.GetValueOrDefault());
            var uniqueConstraints = UniqueConstraints.Count == 0
                ? null
                : UniqueConstraints.Select(static item =>
                    new UniqueConstraintInfo(item.Key, item.Value.ToArray())).ToArray();
            var foreignKeys = ForeignKeys.Count == 0
                ? null
                : ForeignKeys.Select(static item => new ForeignKeyInfo(
                    item.Key,
                    item.Value.SourceColumns.ToArray(),
                    item.Value.TargetSchema,
                    item.Value.TargetTable,
                    item.Value.TargetColumns.ToArray())).ToArray();
            var indexes = Indexes.Count == 0
                ? null
                : Indexes.Select(static item => new IndexInfo(
                    item.Key,
                    item.Value.Columns.ToArray(),
                    item.Value.IsUnique,
                    item.Value.IsClustered)).ToArray();

            return new TableBuildResult(
                new TableSchema(
                    table.SchemaName,
                    table.ObjectName,
                    Columns.ToArray(),
                    primaryKey,
                    uniqueConstraints,
                    foreignKeys,
                    indexes),
                table.IsView);
        }
    }

    private sealed class ForeignKeyAccumulator(string targetSchema, string targetTable)
    {
        internal string TargetSchema { get; } = targetSchema;

        internal string TargetTable { get; } = targetTable;

        internal List<string> SourceColumns { get; } = [];

        internal List<string> TargetColumns { get; } = [];
    }

    private sealed class IndexAccumulator(bool isUnique, bool isClustered)
    {
        internal bool IsUnique { get; } = isUnique;

        internal bool IsClustered { get; } = isClustered;

        internal List<string> Columns { get; } = [];
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
        var accumulators = tables.ToDictionary(
            table => (table.SchemaName, table.ObjectName),
            table => new TableAccumulator(table),
            TableKeyComparer.Instance);
        foreach (var column in columns)
        {
            if (accumulators.TryGetValue((column.SchemaName, column.ObjectName), out var accumulator))
            {
                accumulator.Columns.Add(new ColumnSchema(
                    column.ColumnName,
                    CreateSqlTypeInfo(column.TypeName, column.MaxLength, column.Precision, column.Scale),
                    column.IsNullable,
                    column.IsIdentity,
                    column.IsComputed,
                    column.DefaultExpression,
                    column.Collation));
            }
        }

        foreach (var primaryKey in primaryKeys)
        {
            if (accumulators.TryGetValue((primaryKey.SchemaName, primaryKey.TableName), out var accumulator))
            {
                accumulator.PrimaryKeyIsClustered ??= primaryKey.IsClustered;
                accumulator.PrimaryKeyColumns.Add(primaryKey.ColumnName);
            }
        }

        foreach (var uniqueConstraint in uniqueConstraints)
        {
            if (accumulators.TryGetValue((uniqueConstraint.SchemaName, uniqueConstraint.TableName), out var accumulator))
            {
                if (!accumulator.UniqueConstraints.TryGetValue(uniqueConstraint.ConstraintName, out var constraintColumns))
                {
                    constraintColumns = [];
                    accumulator.UniqueConstraints.Add(uniqueConstraint.ConstraintName, constraintColumns);
                }

                constraintColumns.Add(uniqueConstraint.ColumnName);
            }
        }

        foreach (var foreignKey in foreignKeys)
        {
            if (accumulators.TryGetValue((foreignKey.SchemaName, foreignKey.TableName), out var accumulator))
            {
                if (!accumulator.ForeignKeys.TryGetValue(foreignKey.ForeignKeyName, out var foreignKeyAccumulator))
                {
                    foreignKeyAccumulator = new ForeignKeyAccumulator(foreignKey.TargetSchema, foreignKey.TargetTable);
                    accumulator.ForeignKeys.Add(foreignKey.ForeignKeyName, foreignKeyAccumulator);
                }

                foreignKeyAccumulator.SourceColumns.Add(foreignKey.SourceColumn);
                foreignKeyAccumulator.TargetColumns.Add(foreignKey.TargetColumn);
            }
        }

        foreach (var index in indexes)
        {
            if (accumulators.TryGetValue((index.SchemaName, index.TableName), out var accumulator))
            {
                if (!accumulator.Indexes.TryGetValue(index.IndexName, out var indexAccumulator))
                {
                    indexAccumulator = new IndexAccumulator(index.IsUnique, index.IsClustered);
                    accumulator.Indexes.Add(index.IndexName, indexAccumulator);
                }

                indexAccumulator.Columns.Add(index.ColumnName);
            }
        }

        return tables.Select(table => accumulators[(table.SchemaName, table.ObjectName)].Build()).ToList();
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
