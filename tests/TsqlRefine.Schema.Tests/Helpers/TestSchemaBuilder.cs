using TsqlRefine.Schema.Model;
using TsqlRefine.Schema.Snapshot;
using TsqlRefine.Schema.TypeSystem;

namespace TsqlRefine.Schema.Tests.Helpers;

/// <summary>
/// Fluent builder for creating test schema snapshots with minimal boilerplate.
/// </summary>
public sealed class TestSchemaBuilder
{
    private readonly string _databaseName;
    private readonly List<TableSchema> _tables = [];
    private readonly List<TableSchema> _views = [];

    private TestSchemaBuilder(string databaseName) => _databaseName = databaseName;

    public static TestSchemaBuilder Create(string databaseName = "TestDb") => new(databaseName);

    public TestSchemaBuilder AddTable(string schemaName, string tableName, Action<TableBuilder> configure)
    {
        var builder = new TableBuilder(schemaName, tableName);
        configure(builder);
        _tables.Add(builder.Build());
        return this;
    }

    public TestSchemaBuilder AddView(string schemaName, string viewName, Action<TableBuilder> configure)
    {
        var builder = new TableBuilder(schemaName, viewName);
        configure(builder);
        _views.Add(builder.Build());
        return this;
    }

    public SchemaSnapshot Build()
    {
        var databases = new List<DatabaseSchema>
        {
            new(_databaseName, _tables, _views)
        };
        var hash = SchemaSnapshotSerializer.ComputeContentHash(databases);
        var metadata = new SnapshotMetadata(
            GeneratedAt: "2026-01-01T00:00:00Z",
            ServerName: "localhost",
            DatabaseName: _databaseName,
            CompatLevel: 150,
            ContentHash: hash
        );
        return new SchemaSnapshot(metadata, databases);
    }

    public sealed class TableBuilder
    {
        private readonly string _schemaName;
        private readonly string _tableName;
        private readonly List<ColumnSchema> _columns = [];
        private PrimaryKeyInfo? _primaryKey;
        private readonly List<UniqueConstraintInfo> _uniqueConstraints = [];
        private readonly List<ForeignKeyInfo> _foreignKeys = [];
        private readonly List<IndexInfo> _indexes = [];

        internal TableBuilder(string schemaName, string tableName)
        {
            _schemaName = schemaName;
            _tableName = tableName;
        }

        public TableBuilder AddColumn(
            string name,
            string typeName,
            bool nullable = false,
            int? maxLength = null,
            int? precision = null,
            int? scale = null,
            bool isIdentity = false,
            bool isComputed = false,
            string? defaultExpression = null,
            string? collation = null)
        {
            var category = TypeCategoryMapper.FromTypeName(typeName);
            var typeInfo = new SqlTypeInfo(typeName, category, maxLength, precision, scale);
            _columns.Add(new ColumnSchema(name, typeInfo, nullable, isIdentity, isComputed, defaultExpression, collation));
            return this;
        }

        public TableBuilder WithPrimaryKey(bool isClustered, params string[] columns)
        {
            _primaryKey = new PrimaryKeyInfo(columns, isClustered);
            return this;
        }

        public TableBuilder AddUniqueConstraint(string name, params string[] columns)
        {
            _uniqueConstraints.Add(new UniqueConstraintInfo(name, columns));
            return this;
        }

        public TableBuilder AddForeignKey(string name, string[] sourceColumns, string targetSchema, string targetTable, string[] targetColumns)
        {
            _foreignKeys.Add(new ForeignKeyInfo(name, sourceColumns, targetSchema, targetTable, targetColumns));
            return this;
        }

        public TableBuilder AddIndex(string name, bool isUnique, bool isClustered, params string[] columns)
        {
            _indexes.Add(new IndexInfo(name, columns, isUnique, isClustered));
            return this;
        }

        internal TableSchema Build() => new(
            _schemaName,
            _tableName,
            _columns,
            _primaryKey,
            _uniqueConstraints.Count > 0 ? _uniqueConstraints : null,
            _foreignKeys.Count > 0 ? _foreignKeys : null,
            _indexes.Count > 0 ? _indexes : null
        );
    }
}
