using System.Text;
using TsqlRefine.Schema.Model;
using TsqlRefine.Schema.Snapshot;
using TsqlRefine.Schema.Tests.Helpers;
using TsqlRefine.Schema.TypeSystem;

namespace TsqlRefine.Schema.Tests.Snapshot;

public class SchemaSnapshotSerializerTests
{
    [Fact]
    public void RoundTrip_MinimalSnapshot_Preserves()
    {
        var snapshot = TestSchemaBuilder.Create("TestDb")
            .AddTable("dbo", "Users", t => t
                .AddColumn("Id", "int"))
            .Build();

        var json = SchemaSnapshotSerializer.Serialize(snapshot);
        var deserialized = SchemaSnapshotSerializer.Deserialize(json);

        Assert.Equal(snapshot.Metadata.DatabaseName, deserialized.Metadata.DatabaseName);
        Assert.Equal(snapshot.Metadata.ContentHash, deserialized.Metadata.ContentHash);
        Assert.Single(deserialized.Databases);
        Assert.Single(deserialized.Databases[0].Tables);
        Assert.Equal("Users", deserialized.Databases[0].Tables[0].Name);
    }

    [Fact]
    public void RoundTrip_FullSnapshot_PreservesAllFields()
    {
        var snapshot = TestSchemaBuilder.Create("ProductDb")
            .AddTable("dbo", "Products", t => t
                .AddColumn("Id", "int", isIdentity: true)
                .AddColumn("Name", "nvarchar", maxLength: 200, nullable: false)
                .AddColumn("Price", "decimal", precision: 18, scale: 2)
                .AddColumn("CategoryId", "int", nullable: true)
                .AddColumn("CreatedAt", "datetime2", defaultExpression: "GETUTCDATE()")
                .WithPrimaryKey(true, "Id")
                .AddUniqueConstraint("UQ_Products_Name", "Name")
                .AddForeignKey("FK_Products_Categories", ["CategoryId"], "dbo", "Categories", ["Id"])
                .AddIndex("IX_Products_Category", false, false, "CategoryId"))
            .AddTable("dbo", "Categories", t => t
                .AddColumn("Id", "int", isIdentity: true)
                .AddColumn("Name", "nvarchar", maxLength: 100)
                .WithPrimaryKey(true, "Id"))
            .AddView("dbo", "ActiveProducts", v => v
                .AddColumn("Id", "int")
                .AddColumn("Name", "nvarchar", maxLength: 200))
            .Build();

        var json = SchemaSnapshotSerializer.Serialize(snapshot);
        var deserialized = SchemaSnapshotSerializer.Deserialize(json);

        // Metadata
        Assert.Equal(snapshot.Metadata.GeneratedAt, deserialized.Metadata.GeneratedAt);
        Assert.Equal(snapshot.Metadata.ServerName, deserialized.Metadata.ServerName);
        Assert.Equal(snapshot.Metadata.DatabaseName, deserialized.Metadata.DatabaseName);
        Assert.Equal(snapshot.Metadata.CompatLevel, deserialized.Metadata.CompatLevel);
        Assert.Equal(snapshot.Metadata.ContentHash, deserialized.Metadata.ContentHash);

        // Database
        Assert.Single(deserialized.Databases);
        var db = deserialized.Databases[0];
        Assert.Equal("ProductDb", db.Name);
        Assert.Equal(2, db.Tables.Count);
        Assert.Single(db.Views);

        // Products table
        var products = db.Tables[0];
        Assert.Equal("dbo", products.SchemaName);
        Assert.Equal("Products", products.Name);
        Assert.Equal(5, products.Columns.Count);

        // Column details
        var idCol = products.Columns[0];
        Assert.Equal("Id", idCol.Name);
        Assert.Equal("int", idCol.Type.TypeName);
        Assert.Equal(TypeCategory.ExactNumeric, idCol.Type.Category);
        Assert.True(idCol.IsIdentity);
        Assert.False(idCol.IsNullable);

        var priceCol = products.Columns[2];
        Assert.Equal(18, priceCol.Type.Precision);
        Assert.Equal(2, priceCol.Type.Scale);

        var createdAtCol = products.Columns[4];
        Assert.Equal("GETUTCDATE()", createdAtCol.DefaultExpression);

        // Constraints
        Assert.NotNull(products.PrimaryKey);
        Assert.True(products.PrimaryKey!.IsClustered);
        Assert.Equal(["Id"], products.PrimaryKey.Columns);

        Assert.Single(products.UniqueConstraints!);
        Assert.Equal("UQ_Products_Name", products.UniqueConstraints![0].Name);

        Assert.Single(products.ForeignKeys!);
        var fk = products.ForeignKeys![0];
        Assert.Equal("FK_Products_Categories", fk.Name);
        Assert.Equal(["CategoryId"], fk.SourceColumns);
        Assert.Equal("dbo", fk.TargetSchema);
        Assert.Equal("Categories", fk.TargetTable);
        Assert.Equal(["Id"], fk.TargetColumns);

        Assert.Single(products.Indexes!);
        Assert.Equal("IX_Products_Category", products.Indexes![0].Name);

        // View
        var view = db.Views[0];
        Assert.Equal("ActiveProducts", view.Name);
        Assert.Equal(2, view.Columns.Count);
    }

    [Fact]
    public void RoundTrip_EmptyDatabases_Preserves()
    {
        var metadata = new SnapshotMetadata("2026-01-01T00:00:00Z", "srv", "EmptyDb", 150, "empty");
        var snapshot = new SchemaSnapshot(metadata, [new DatabaseSchema("EmptyDb", [], [])]);

        var json = SchemaSnapshotSerializer.Serialize(snapshot);
        var deserialized = SchemaSnapshotSerializer.Deserialize(json);

        Assert.Single(deserialized.Databases);
        Assert.Empty(deserialized.Databases[0].Tables);
        Assert.Empty(deserialized.Databases[0].Views);
    }

    [Fact]
    public void Serialize_ProducesCamelCaseJson()
    {
        var snapshot = TestSchemaBuilder.Create()
            .AddTable("dbo", "Test", t => t
                .AddColumn("Id", "int"))
            .Build();

        var json = SchemaSnapshotSerializer.Serialize(snapshot);

        Assert.Contains("\"generatedAt\"", json);
        Assert.Contains("\"serverName\"", json);
        Assert.Contains("\"databaseName\"", json);
        Assert.Contains("\"compatLevel\"", json);
        Assert.Contains("\"contentHash\"", json);
        Assert.Contains("\"schemaName\"", json);
        Assert.Contains("\"typeName\"", json);
        Assert.Contains("\"isNullable\"", json);
    }

    [Fact]
    public void Serialize_EnumValues_AreCamelCase()
    {
        var snapshot = TestSchemaBuilder.Create()
            .AddTable("dbo", "Test", t => t
                .AddColumn("Id", "int")
                .AddColumn("Name", "nvarchar", maxLength: 100))
            .Build();

        var json = SchemaSnapshotSerializer.Serialize(snapshot);

        Assert.Contains("\"exactNumeric\"", json);
        Assert.Contains("\"unicodeString\"", json);
    }

    [Fact]
    public void Serialize_NullFields_AreOmitted()
    {
        var snapshot = TestSchemaBuilder.Create()
            .AddTable("dbo", "Test", t => t
                .AddColumn("Id", "int"))
            .Build();

        var json = SchemaSnapshotSerializer.Serialize(snapshot);

        Assert.DoesNotContain("\"primaryKey\"", json);
        Assert.DoesNotContain("\"uniqueConstraints\"", json);
        Assert.DoesNotContain("\"foreignKeys\"", json);
        Assert.DoesNotContain("\"indexes\"", json);
        Assert.DoesNotContain("\"defaultExpression\"", json);
        Assert.DoesNotContain("\"collation\"", json);
    }

    [Fact]
    public void ComputeContentHash_SameContent_SameHash()
    {
        var col = new ColumnSchema("Id", new SqlTypeInfo("int", TypeCategory.ExactNumeric), false);
        var databases1 = new List<DatabaseSchema> { new("Db", [new TableSchema("dbo", "T", [col])], []) };
        var databases2 = new List<DatabaseSchema> { new("Db", [new TableSchema("dbo", "T", [col])], []) };

        var hash1 = SchemaSnapshotSerializer.ComputeContentHash(databases1);
        var hash2 = SchemaSnapshotSerializer.ComputeContentHash(databases2);

        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void ComputeContentHash_DifferentContent_DifferentHash()
    {
        var col1 = new ColumnSchema("Id", new SqlTypeInfo("int", TypeCategory.ExactNumeric), false);
        var col2 = new ColumnSchema("Name", new SqlTypeInfo("nvarchar", TypeCategory.UnicodeString, MaxLength: 100), true);
        var databases1 = new List<DatabaseSchema> { new("Db", [new TableSchema("dbo", "T", [col1])], []) };
        var databases2 = new List<DatabaseSchema> { new("Db", [new TableSchema("dbo", "T", [col2])], []) };

        var hash1 = SchemaSnapshotSerializer.ComputeContentHash(databases1);
        var hash2 = SchemaSnapshotSerializer.ComputeContentHash(databases2);

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void ComputeContentHash_Returns64CharHexString()
    {
        var databases = new List<DatabaseSchema> { new("Db", [], []) };
        var hash = SchemaSnapshotSerializer.ComputeContentHash(databases);

        Assert.Equal(64, hash.Length);
        Assert.Matches("^[0-9a-f]{64}$", hash);
    }

    [Fact]
    public void Deserialize_InvalidJson_ThrowsJsonException()
    {
        Assert.Throws<System.Text.Json.JsonException>(() =>
            SchemaSnapshotSerializer.Deserialize("not valid json"));
    }

    [Fact]
    public void Deserialize_Stream_ValidJson_ReturnsSnapshot()
    {
        var snapshot = TestSchemaBuilder.Create("TestDb")
            .AddTable("dbo", "Users", t => t
                .AddColumn("Id", "int"))
            .Build();
        var json = SchemaSnapshotSerializer.Serialize(snapshot);

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var deserialized = SchemaSnapshotSerializer.Deserialize(stream);

        Assert.Equal(snapshot.Metadata.DatabaseName, deserialized.Metadata.DatabaseName);
        Assert.Single(deserialized.Databases);
        Assert.Single(deserialized.Databases[0].Tables);
    }

    [Fact]
    public void Serialize_NullSnapshot_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            SchemaSnapshotSerializer.Serialize(null!));
    }

    [Fact]
    public void Deserialize_NullJson_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            SchemaSnapshotSerializer.Deserialize((string)null!));
    }
}
