using TsqlRefine.PluginSdk;
using TsqlRefine.Schema.Resolution;
using TsqlRefine.Schema.Tests.Helpers;

namespace TsqlRefine.Schema.Tests.Resolution;

public class SchemaProviderTests
{
    private static SchemaProvider CreateProvider()
    {
        var snapshot = TestSchemaBuilder.Create("TestDb")
            .AddTable("dbo", "Users", t => t
                .AddColumn("Id", "int", isIdentity: true)
                .AddColumn("Name", "nvarchar", maxLength: 100)
                .AddColumn("Email", "nvarchar", maxLength: 256, nullable: true)
                .AddColumn("RoleId", "int"))
            .AddTable("dbo", "Orders", t => t
                .AddColumn("OrderId", "int", isIdentity: true)
                .AddColumn("UserId", "int")
                .AddColumn("Amount", "decimal", precision: 18, scale: 2))
            .AddTable("sales", "Orders", t => t
                .AddColumn("Id", "int", isIdentity: true)
                .AddColumn("Total", "money"))
            .AddView("dbo", "ActiveUsers", v => v
                .AddColumn("Id", "int")
                .AddColumn("Name", "nvarchar", maxLength: 100))
            .Build();

        return new SchemaProvider(snapshot);
    }

    // --- Table Resolution ---

    [Fact]
    public void ResolveTable_OnePartName_UsesDefaultSchema()
    {
        var provider = CreateProvider();
        var result = provider.ResolveTable(null, null, "Users");

        Assert.NotNull(result);
        Assert.Equal("dbo", result!.SchemaName);
        Assert.Equal("Users", result.TableName);
        Assert.False(result.IsView);
    }

    [Fact]
    public void ResolveTable_TwoPartName_UsesSpecifiedSchema()
    {
        var provider = CreateProvider();
        var result = provider.ResolveTable(null, "sales", "Orders");

        Assert.NotNull(result);
        Assert.Equal("sales", result!.SchemaName);
        Assert.Equal("Orders", result.TableName);
    }

    [Fact]
    public void ResolveTable_ThreePartName_UsesSpecifiedDatabase()
    {
        var provider = CreateProvider();
        var result = provider.ResolveTable("TestDb", "dbo", "Users");

        Assert.NotNull(result);
        Assert.Equal("TestDb", result!.DatabaseName);
        Assert.Equal("dbo", result.SchemaName);
        Assert.Equal("Users", result.TableName);
    }

    [Fact]
    public void ResolveTable_NonExistentTable_ReturnsNull()
    {
        var provider = CreateProvider();
        var result = provider.ResolveTable(null, null, "NonExistent");

        Assert.Null(result);
    }

    [Fact]
    public void ResolveTable_NonExistentSchema_ReturnsNull()
    {
        var provider = CreateProvider();
        var result = provider.ResolveTable(null, "nonexistent", "Users");

        Assert.Null(result);
    }

    [Fact]
    public void ResolveTable_NonExistentDatabase_ReturnsNull()
    {
        var provider = CreateProvider();
        var result = provider.ResolveTable("OtherDb", "dbo", "Users");

        Assert.Null(result);
    }

    [Fact]
    public void ResolveTable_CaseInsensitive_TableName()
    {
        var provider = CreateProvider();

        var upper = provider.ResolveTable(null, null, "USERS");
        var lower = provider.ResolveTable(null, null, "users");
        var mixed = provider.ResolveTable(null, null, "Users");

        Assert.NotNull(upper);
        Assert.NotNull(lower);
        Assert.NotNull(mixed);
    }

    [Fact]
    public void ResolveTable_CaseInsensitive_SchemaName()
    {
        var provider = CreateProvider();

        var upper = provider.ResolveTable(null, "DBO", "Users");
        var lower = provider.ResolveTable(null, "dbo", "Users");

        Assert.NotNull(upper);
        Assert.NotNull(lower);
    }

    [Fact]
    public void ResolveTable_CaseInsensitive_DatabaseName()
    {
        var provider = CreateProvider();

        var upper = provider.ResolveTable("TESTDB", "dbo", "Users");
        var lower = provider.ResolveTable("testdb", "dbo", "Users");

        Assert.NotNull(upper);
        Assert.NotNull(lower);
    }

    [Fact]
    public void ResolveTable_View_ResolvesCorrectly()
    {
        var provider = CreateProvider();
        var result = provider.ResolveTable(null, null, "ActiveUsers");

        Assert.NotNull(result);
        Assert.True(result!.IsView);
        Assert.Equal("ActiveUsers", result.TableName);
    }

    [Fact]
    public void ResolveTable_SameNameDifferentSchemas_ResolvesCorrectly()
    {
        var provider = CreateProvider();

        var dboOrders = provider.ResolveTable(null, "dbo", "Orders");
        var salesOrders = provider.ResolveTable(null, "sales", "Orders");

        Assert.NotNull(dboOrders);
        Assert.NotNull(salesOrders);
        Assert.Equal("dbo", dboOrders!.SchemaName);
        Assert.Equal("sales", salesOrders!.SchemaName);
    }

    // --- Column Resolution ---

    [Fact]
    public void ResolveColumn_ExistingColumn_ReturnsColumn()
    {
        var provider = CreateProvider();
        var table = provider.ResolveTable(null, null, "Users")!;
        var column = provider.ResolveColumn(table, "Id");

        Assert.NotNull(column);
        Assert.Equal("Id", column!.Column.Name);
        Assert.Equal("int", column.Column.Type.TypeName);
        Assert.Equal(SchemaTypeCategory.ExactNumeric, column.Column.Type.Category);
    }

    [Fact]
    public void ResolveColumn_NonExistentColumn_ReturnsNull()
    {
        var provider = CreateProvider();
        var table = provider.ResolveTable(null, null, "Users")!;
        var column = provider.ResolveColumn(table, "NonExistent");

        Assert.Null(column);
    }

    [Fact]
    public void ResolveColumn_CaseInsensitive()
    {
        var provider = CreateProvider();
        var table = provider.ResolveTable(null, null, "Users")!;

        var upper = provider.ResolveColumn(table, "ID");
        var lower = provider.ResolveColumn(table, "id");
        var mixed = provider.ResolveColumn(table, "Id");

        Assert.NotNull(upper);
        Assert.NotNull(lower);
        Assert.NotNull(mixed);
    }

    [Fact]
    public void ResolveColumn_NullableColumn_PreservesNullability()
    {
        var provider = CreateProvider();
        var table = provider.ResolveTable(null, null, "Users")!;
        var email = provider.ResolveColumn(table, "Email");

        Assert.NotNull(email);
        Assert.True(email!.Column.IsNullable);
    }

    [Fact]
    public void ResolveColumn_IdentityColumn_PreservesIdentity()
    {
        var provider = CreateProvider();
        var table = provider.ResolveTable(null, null, "Users")!;
        var id = provider.ResolveColumn(table, "Id");

        Assert.NotNull(id);
        Assert.True(id!.Column.IsIdentity);
    }

    [Fact]
    public void ResolveColumn_WithTypeDetails_PreservesPrecisionAndScale()
    {
        var provider = CreateProvider();
        var table = provider.ResolveTable(null, null, "Orders")!;
        var amount = provider.ResolveColumn(table, "Amount");

        Assert.NotNull(amount);
        Assert.Equal("decimal", amount!.Column.Type.TypeName);
        Assert.Equal(18, amount.Column.Type.Precision);
        Assert.Equal(2, amount.Column.Type.Scale);
    }

    [Fact]
    public void ResolveColumn_OnView_Works()
    {
        var provider = CreateProvider();
        var view = provider.ResolveTable(null, null, "ActiveUsers")!;
        var col = provider.ResolveColumn(view, "Name");

        Assert.NotNull(col);
        Assert.Equal("Name", col!.Column.Name);
        Assert.True(col.Table.IsView);
    }

    // --- GetColumns ---

    [Fact]
    public void GetColumns_ReturnsAllColumns()
    {
        var provider = CreateProvider();
        var table = provider.ResolveTable(null, null, "Users")!;
        var columns = provider.GetColumns(table);

        Assert.Equal(4, columns.Count);
        Assert.Equal("Id", columns[0].Name);
        Assert.Equal("Name", columns[1].Name);
        Assert.Equal("Email", columns[2].Name);
        Assert.Equal("RoleId", columns[3].Name);
    }

    [Fact]
    public void GetColumns_NonExistentTable_ReturnsEmpty()
    {
        var provider = CreateProvider();
        var fakeTable = new ResolvedTable("TestDb", "dbo", "FakeTable", false);
        var columns = provider.GetColumns(fakeTable);

        Assert.Empty(columns);
    }

    // --- Metadata ---

    [Fact]
    public void Metadata_ReturnsSnapshotMetadata()
    {
        var provider = CreateProvider();
        var metadata = provider.Metadata;

        Assert.Equal("TestDb", metadata.DatabaseName);
        Assert.Equal("localhost", metadata.ServerName);
        Assert.Equal(150, metadata.CompatLevel);
        Assert.NotEmpty(metadata.ContentHash);
    }

    // --- DefaultSchema ---

    [Fact]
    public void DefaultSchema_ReturnsConfiguredDefault()
    {
        var provider = CreateProvider();
        Assert.Equal("dbo", provider.DefaultSchema);
    }

    [Fact]
    public void DefaultSchema_CustomDefault_Works()
    {
        var snapshot = TestSchemaBuilder.Create("TestDb")
            .AddTable("custom", "Items", t => t
                .AddColumn("Id", "int"))
            .Build();

        var provider = new SchemaProvider(snapshot, defaultSchema: "custom");
        Assert.Equal("custom", provider.DefaultSchema);

        var result = provider.ResolveTable(null, null, "Items");
        Assert.NotNull(result);
        Assert.Equal("custom", result!.SchemaName);
    }

    // --- Edge Cases ---

    [Fact]
    public void ResolveTable_EmptySnapshot_ReturnsNull()
    {
        var snapshot = TestSchemaBuilder.Create("EmptyDb").Build();
        var provider = new SchemaProvider(snapshot);

        var result = provider.ResolveTable(null, null, "Users");
        Assert.Null(result);
    }

    [Fact]
    public void Constructor_NullSnapshot_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new SchemaProvider(null!));
    }

    [Fact]
    public void ResolveTable_NullName_ThrowsArgumentNullException()
    {
        var provider = CreateProvider();
        Assert.Throws<ArgumentNullException>(() => provider.ResolveTable(null, null, null!));
    }

    [Fact]
    public void ResolveColumn_NullTable_ThrowsArgumentNullException()
    {
        var provider = CreateProvider();
        Assert.Throws<ArgumentNullException>(() => provider.ResolveColumn(null!, "Id"));
    }

    [Fact]
    public void ResolveColumn_NullColumnName_ThrowsArgumentNullException()
    {
        var provider = CreateProvider();
        var table = provider.ResolveTable(null, null, "Users")!;
        Assert.Throws<ArgumentNullException>(() => provider.ResolveColumn(table, null!));
    }

    [Fact]
    public void GetColumns_NullTable_ThrowsArgumentNullException()
    {
        var provider = CreateProvider();
        Assert.Throws<ArgumentNullException>(() => provider.GetColumns(null!));
    }
}
