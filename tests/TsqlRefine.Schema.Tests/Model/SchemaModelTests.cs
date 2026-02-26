using TsqlRefine.Schema.Model;
using TsqlRefine.Schema.Snapshot;
using TsqlRefine.Schema.Tests.Helpers;
using TsqlRefine.Schema.TypeSystem;

namespace TsqlRefine.Schema.Tests.Model;

public class SchemaModelTests
{
    [Fact]
    public void ColumnSchema_RecordEquality_Works()
    {
        var type = new SqlTypeInfo("int", TypeCategory.ExactNumeric);
        var col1 = new ColumnSchema("Id", type, false);
        var col2 = new ColumnSchema("Id", type, false);

        Assert.Equal(col1, col2);
    }

    [Fact]
    public void ColumnSchema_RecordInequality_DifferentName()
    {
        var type = new SqlTypeInfo("int", TypeCategory.ExactNumeric);
        var col1 = new ColumnSchema("Id", type, false);
        var col2 = new ColumnSchema("Name", type, false);

        Assert.NotEqual(col1, col2);
    }

    [Fact]
    public void SqlTypeInfo_WithMaxLength_PreservesValue()
    {
        var type = new SqlTypeInfo("nvarchar", TypeCategory.UnicodeString, MaxLength: 200);

        Assert.Equal("nvarchar", type.TypeName);
        Assert.Equal(TypeCategory.UnicodeString, type.Category);
        Assert.Equal(200, type.MaxLength);
        Assert.Null(type.Precision);
        Assert.Null(type.Scale);
    }

    [Fact]
    public void SqlTypeInfo_WithPrecisionAndScale_PreservesValues()
    {
        var type = new SqlTypeInfo("decimal", TypeCategory.ExactNumeric, Precision: 18, Scale: 2);

        Assert.Equal(18, type.Precision);
        Assert.Equal(2, type.Scale);
    }

    [Fact]
    public void TableSchema_MinimalConfiguration_Works()
    {
        var columns = new[]
        {
            new ColumnSchema("Id", new SqlTypeInfo("int", TypeCategory.ExactNumeric), false)
        };
        var table = new TableSchema("dbo", "Users", columns);

        Assert.Equal("dbo", table.SchemaName);
        Assert.Equal("Users", table.Name);
        Assert.Single(table.Columns);
        Assert.Null(table.PrimaryKey);
        Assert.Null(table.UniqueConstraints);
        Assert.Null(table.ForeignKeys);
        Assert.Null(table.Indexes);
    }

    [Fact]
    public void TableSchema_FullConfiguration_PreservesAll()
    {
        var columns = new[]
        {
            new ColumnSchema("Id", new SqlTypeInfo("int", TypeCategory.ExactNumeric), false, IsIdentity: true),
            new ColumnSchema("Name", new SqlTypeInfo("nvarchar", TypeCategory.UnicodeString, MaxLength: 100), true)
        };
        var pk = new PrimaryKeyInfo(["Id"], true);
        var unique = new[] { new UniqueConstraintInfo("UQ_Name", ["Name"]) };
        var fk = new[] { new ForeignKeyInfo("FK_Users_Roles", ["RoleId"], "dbo", "Roles", ["Id"]) };
        var idx = new[] { new IndexInfo("IX_Name", ["Name"], false, false) };

        var table = new TableSchema("dbo", "Users", columns, pk, unique, fk, idx);

        Assert.NotNull(table.PrimaryKey);
        Assert.True(table.PrimaryKey!.IsClustered);
        Assert.Single(table.UniqueConstraints!);
        Assert.Single(table.ForeignKeys!);
        Assert.Single(table.Indexes!);
    }

    [Fact]
    public void DatabaseSchema_ContainsTablesAndViews()
    {
        var col = new ColumnSchema("Id", new SqlTypeInfo("int", TypeCategory.ExactNumeric), false);
        var table = new TableSchema("dbo", "Users", [col]);
        var view = new TableSchema("dbo", "ActiveUsers", [col]);
        var db = new DatabaseSchema("TestDb", [table], [view]);

        Assert.Equal("TestDb", db.Name);
        Assert.Single(db.Tables);
        Assert.Single(db.Views);
    }

    [Fact]
    public void SchemaSnapshot_WithMetadata_PreservesAll()
    {
        var metadata = new SnapshotMetadata(
            "2026-01-01T00:00:00Z", "localhost", "TestDb", 150, "abc123");
        var col = new ColumnSchema("Id", new SqlTypeInfo("int", TypeCategory.ExactNumeric), false);
        var db = new DatabaseSchema("TestDb", [new TableSchema("dbo", "Users", [col])], []);
        var snapshot = new SchemaSnapshot(metadata, [db]);

        Assert.Equal("2026-01-01T00:00:00Z", snapshot.Metadata.GeneratedAt);
        Assert.Equal("localhost", snapshot.Metadata.ServerName);
        Assert.Equal(150, snapshot.Metadata.CompatLevel);
        Assert.Single(snapshot.Databases);
    }

    [Fact]
    public void TestSchemaBuilder_CreatesValidSnapshot()
    {
        var snapshot = TestSchemaBuilder.Create("MyDb")
            .AddTable("dbo", "Users", t => t
                .AddColumn("Id", "int", isIdentity: true)
                .AddColumn("Name", "nvarchar", maxLength: 100)
                .AddColumn("Email", "nvarchar", maxLength: 256, nullable: true)
                .WithPrimaryKey(true, "Id"))
            .AddTable("dbo", "Orders", t => t
                .AddColumn("OrderId", "int", isIdentity: true)
                .AddColumn("UserId", "int")
                .AddForeignKey("FK_Orders_Users", ["UserId"], "dbo", "Users", ["Id"]))
            .Build();

        Assert.Equal("MyDb", snapshot.Metadata.DatabaseName);
        Assert.Single(snapshot.Databases);
        Assert.Equal(2, snapshot.Databases[0].Tables.Count);

        var usersTable = snapshot.Databases[0].Tables[0];
        Assert.Equal("Users", usersTable.Name);
        Assert.Equal(3, usersTable.Columns.Count);
        Assert.NotNull(usersTable.PrimaryKey);

        var ordersTable = snapshot.Databases[0].Tables[1];
        Assert.Single(ordersTable.ForeignKeys!);
    }

    [Fact]
    public void TestSchemaBuilder_AddView_Works()
    {
        var snapshot = TestSchemaBuilder.Create()
            .AddView("dbo", "ActiveUsers", v => v
                .AddColumn("Id", "int")
                .AddColumn("Name", "nvarchar", maxLength: 100))
            .Build();

        Assert.Empty(snapshot.Databases[0].Tables);
        Assert.Single(snapshot.Databases[0].Views);
        Assert.Equal("ActiveUsers", snapshot.Databases[0].Views[0].Name);
    }

    [Fact]
    public void TestSchemaBuilder_ColumnTypeCategory_CorrectlyMapped()
    {
        var snapshot = TestSchemaBuilder.Create()
            .AddTable("dbo", "TypeTest", t => t
                .AddColumn("IntCol", "int")
                .AddColumn("VarcharCol", "varchar", maxLength: 50)
                .AddColumn("NVarcharCol", "nvarchar", maxLength: 100)
                .AddColumn("DateCol", "datetime2")
                .AddColumn("GuidCol", "uniqueidentifier")
                .AddColumn("DecimalCol", "decimal", precision: 18, scale: 2))
            .Build();

        var table = snapshot.Databases[0].Tables[0];
        Assert.Equal(TypeCategory.ExactNumeric, table.Columns[0].Type.Category);
        Assert.Equal(TypeCategory.AnsiString, table.Columns[1].Type.Category);
        Assert.Equal(TypeCategory.UnicodeString, table.Columns[2].Type.Category);
        Assert.Equal(TypeCategory.DateTime, table.Columns[3].Type.Category);
        Assert.Equal(TypeCategory.UniqueIdentifier, table.Columns[4].Type.Category);
        Assert.Equal(TypeCategory.ExactNumeric, table.Columns[5].Type.Category);
    }
}
