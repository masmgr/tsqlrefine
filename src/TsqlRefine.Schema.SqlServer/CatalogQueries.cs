namespace TsqlRefine.Schema.SqlServer;

/// <summary>
/// SQL queries for extracting schema metadata from sys.* catalog views.
/// </summary>
internal static class CatalogQueries
{
    internal const string TablesAndViews = """
        SELECT
            s.name AS SchemaName,
            o.name AS ObjectName,
            o.type_desc AS TypeDesc
        FROM sys.objects o
        JOIN sys.schemas s ON o.schema_id = s.schema_id
        WHERE o.type IN ('U', 'V')
        ORDER BY s.name, o.name
        """;

    internal const string Columns = """
        SELECT
            s.name AS SchemaName,
            o.name AS ObjectName,
            c.name AS ColumnName,
            t.name AS TypeName,
            c.max_length AS MaxLength,
            c.precision AS [Precision],
            c.scale AS Scale,
            c.is_nullable AS IsNullable,
            c.is_identity AS IsIdentity,
            c.is_computed AS IsComputed,
            dc.definition AS DefaultExpression,
            c.collation_name AS Collation,
            c.column_id AS ColumnId
        FROM sys.columns c
        JOIN sys.objects o ON c.object_id = o.object_id
        JOIN sys.schemas s ON o.schema_id = s.schema_id
        JOIN sys.types t ON c.user_type_id = t.user_type_id
        LEFT JOIN sys.default_constraints dc ON c.default_object_id = dc.object_id
        WHERE o.type IN ('U', 'V')
        ORDER BY s.name, o.name, c.column_id
        """;

    internal const string PrimaryKeys = """
        SELECT
            s.name AS SchemaName,
            o.name AS TableName,
            i.is_primary_key AS IsPrimaryKey,
            i.type_desc AS IndexType,
            ic.key_ordinal AS KeyOrdinal,
            col.name AS ColumnName
        FROM sys.indexes i
        JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
        JOIN sys.columns col ON ic.object_id = col.object_id AND ic.column_id = col.column_id
        JOIN sys.objects o ON i.object_id = o.object_id
        JOIN sys.schemas s ON o.schema_id = s.schema_id
        WHERE i.is_primary_key = 1 AND o.type = 'U'
        ORDER BY s.name, o.name, ic.key_ordinal
        """;

    internal const string UniqueConstraints = """
        SELECT
            s.name AS SchemaName,
            o.name AS TableName,
            i.name AS ConstraintName,
            ic.key_ordinal AS KeyOrdinal,
            col.name AS ColumnName
        FROM sys.indexes i
        JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
        JOIN sys.columns col ON ic.object_id = col.object_id AND ic.column_id = col.column_id
        JOIN sys.objects o ON i.object_id = o.object_id
        JOIN sys.schemas s ON o.schema_id = s.schema_id
        WHERE i.is_unique_constraint = 1 AND o.type = 'U'
        ORDER BY s.name, o.name, i.name, ic.key_ordinal
        """;

    internal const string ForeignKeys = """
        SELECT
            s.name AS SchemaName,
            o.name AS TableName,
            fk.name AS ForeignKeyName,
            fkc.constraint_column_id AS ColumnOrdinal,
            srcCol.name AS SourceColumn,
            tgtSchema.name AS TargetSchema,
            tgtTable.name AS TargetTable,
            tgtCol.name AS TargetColumn
        FROM sys.foreign_keys fk
        JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
        JOIN sys.objects o ON fk.parent_object_id = o.object_id
        JOIN sys.schemas s ON o.schema_id = s.schema_id
        JOIN sys.columns srcCol ON fkc.parent_object_id = srcCol.object_id AND fkc.parent_column_id = srcCol.column_id
        JOIN sys.objects tgtTable ON fkc.referenced_object_id = tgtTable.object_id
        JOIN sys.schemas tgtSchema ON tgtTable.schema_id = tgtSchema.schema_id
        JOIN sys.columns tgtCol ON fkc.referenced_object_id = tgtCol.object_id AND fkc.referenced_column_id = tgtCol.column_id
        ORDER BY s.name, o.name, fk.name, fkc.constraint_column_id
        """;

    internal const string Indexes = """
        SELECT
            s.name AS SchemaName,
            o.name AS TableName,
            i.name AS IndexName,
            i.is_unique AS IsUnique,
            i.type_desc AS IndexType,
            ic.key_ordinal AS KeyOrdinal,
            col.name AS ColumnName
        FROM sys.indexes i
        JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
        JOIN sys.columns col ON ic.object_id = col.object_id AND ic.column_id = col.column_id
        JOIN sys.objects o ON i.object_id = o.object_id
        JOIN sys.schemas s ON o.schema_id = s.schema_id
        WHERE i.is_primary_key = 0
          AND i.is_unique_constraint = 0
          AND i.type > 0
          AND o.type = 'U'
          AND ic.is_included_column = 0
        ORDER BY s.name, o.name, i.name, ic.key_ordinal
        """;

    internal const string DatabaseInfo = """
        SELECT
            DB_NAME() AS DatabaseName,
            SERVERPROPERTY('ServerName') AS ServerName,
            compatibility_level AS CompatLevel
        FROM sys.databases
        WHERE database_id = DB_ID()
        """;
}
