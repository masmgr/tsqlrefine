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

    // --- ER Information: Helper to create a provider with constraints ---

    private static SchemaProvider CreateErProvider()
    {
        var snapshot = TestSchemaBuilder.Create("TestDb")
            .AddTable("dbo", "Users", t => t
                .AddColumn("Id", "int", isIdentity: true)
                .AddColumn("Name", "nvarchar", maxLength: 100)
                .AddColumn("Email", "nvarchar", maxLength: 256, nullable: true)
                .WithPrimaryKey(true, "Id")
                .AddUniqueConstraint("UQ_Users_Email", "Email"))
            .AddTable("dbo", "Orders", t => t
                .AddColumn("OrderId", "int", isIdentity: true)
                .AddColumn("UserId", "int")
                .AddColumn("Amount", "decimal", precision: 18, scale: 2)
                .WithPrimaryKey(true, "OrderId")
                .AddForeignKey("FK_Orders_Users", ["UserId"], "dbo", "Users", ["Id"]))
            .AddTable("dbo", "OrderItems", t => t
                .AddColumn("OrderId", "int")
                .AddColumn("ProductId", "int")
                .AddColumn("Quantity", "int")
                .WithPrimaryKey(true, "OrderId", "ProductId")
                .AddForeignKey("FK_OrderItems_Orders", ["OrderId"], "dbo", "Orders", ["OrderId"])
                .AddForeignKey("FK_OrderItems_Products", ["ProductId"], "dbo", "Products", ["ProductId"]))
            .AddTable("dbo", "Products", t => t
                .AddColumn("ProductId", "int", isIdentity: true)
                .AddColumn("Sku", "varchar", maxLength: 50)
                .WithPrimaryKey(true, "ProductId")
                .AddIndex("IX_Products_Sku", isUnique: true, isClustered: false, "Sku"))
            .AddTable("dbo", "NoPkTable", t => t
                .AddColumn("Col1", "int")
                .AddColumn("Col2", "varchar", maxLength: 50))
            .Build();

        return new SchemaProvider(snapshot);
    }

    // --- GetPrimaryKey ---

    [Fact]
    public void GetPrimaryKey_TableWithPk_ReturnsPk()
    {
        var provider = CreateErProvider();
        var table = provider.ResolveTable(null, null, "Users")!;
        var pk = provider.GetPrimaryKey(table);

        Assert.NotNull(pk);
        Assert.Single(pk!.Columns);
        Assert.Equal("Id", pk.Columns[0]);
        Assert.True(pk.IsClustered);
    }

    [Fact]
    public void GetPrimaryKey_CompositePk_ReturnsAllColumns()
    {
        var provider = CreateErProvider();
        var table = provider.ResolveTable(null, null, "OrderItems")!;
        var pk = provider.GetPrimaryKey(table);

        Assert.NotNull(pk);
        Assert.Equal(2, pk!.Columns.Count);
        Assert.Equal("OrderId", pk.Columns[0]);
        Assert.Equal("ProductId", pk.Columns[1]);
    }

    [Fact]
    public void GetPrimaryKey_NoPk_ReturnsNull()
    {
        var provider = CreateErProvider();
        var table = provider.ResolveTable(null, null, "NoPkTable")!;
        var pk = provider.GetPrimaryKey(table);

        Assert.Null(pk);
    }

    [Fact]
    public void GetPrimaryKey_NonExistentTable_ReturnsNull()
    {
        var provider = CreateErProvider();
        var fakeTable = new ResolvedTable("TestDb", "dbo", "FakeTable", false);
        var pk = provider.GetPrimaryKey(fakeTable);

        Assert.Null(pk);
    }

    // --- GetUniqueConstraints ---

    [Fact]
    public void GetUniqueConstraints_WithUniqueConstraint_ReturnsConstraints()
    {
        var provider = CreateErProvider();
        var table = provider.ResolveTable(null, null, "Users")!;
        var ucs = provider.GetUniqueConstraints(table);

        Assert.Single(ucs);
        Assert.Equal("UQ_Users_Email", ucs[0].Name);
        Assert.Single(ucs[0].Columns);
        Assert.Equal("Email", ucs[0].Columns[0]);
    }

    [Fact]
    public void GetUniqueConstraints_WithUniqueIndex_IncludesIndex()
    {
        var provider = CreateErProvider();
        var table = provider.ResolveTable(null, null, "Products")!;
        var ucs = provider.GetUniqueConstraints(table);

        Assert.Single(ucs);
        Assert.Equal("IX_Products_Sku", ucs[0].Name);
        Assert.Equal("Sku", ucs[0].Columns[0]);
    }

    [Fact]
    public void GetUniqueConstraints_NoConstraints_ReturnsEmpty()
    {
        var provider = CreateErProvider();
        var table = provider.ResolveTable(null, null, "NoPkTable")!;
        var ucs = provider.GetUniqueConstraints(table);

        Assert.Empty(ucs);
    }

    [Fact]
    public void GetUniqueConstraints_NonExistentTable_ReturnsEmpty()
    {
        var provider = CreateErProvider();
        var fakeTable = new ResolvedTable("TestDb", "dbo", "FakeTable", false);
        var ucs = provider.GetUniqueConstraints(fakeTable);

        Assert.Empty(ucs);
    }

    // --- GetForeignKeys ---

    [Fact]
    public void GetForeignKeys_TableWithFk_ReturnsFks()
    {
        var provider = CreateErProvider();
        var table = provider.ResolveTable(null, null, "Orders")!;
        var fks = provider.GetForeignKeys(table);

        Assert.Single(fks);
        Assert.Equal("FK_Orders_Users", fks[0].Name);
        Assert.Equal("UserId", fks[0].SourceColumns[0]);
        Assert.Equal("Users", fks[0].TargetTable.TableName);
        Assert.Equal("Id", fks[0].TargetColumns[0]);
    }

    [Fact]
    public void GetForeignKeys_MultipleFks_ReturnsAll()
    {
        var provider = CreateErProvider();
        var table = provider.ResolveTable(null, null, "OrderItems")!;
        var fks = provider.GetForeignKeys(table);

        Assert.Equal(2, fks.Count);
        Assert.Contains(fks, fk => fk.Name == "FK_OrderItems_Orders");
        Assert.Contains(fks, fk => fk.Name == "FK_OrderItems_Products");
    }

    [Fact]
    public void GetForeignKeys_NoFks_ReturnsEmpty()
    {
        var provider = CreateErProvider();
        var table = provider.ResolveTable(null, null, "Users")!;
        var fks = provider.GetForeignKeys(table);

        Assert.Empty(fks);
    }

    [Fact]
    public void GetForeignKeys_SourceTableIsCorrect()
    {
        var provider = CreateErProvider();
        var table = provider.ResolveTable(null, null, "Orders")!;
        var fks = provider.GetForeignKeys(table);

        Assert.Single(fks);
        Assert.Equal("Orders", fks[0].SourceTable.TableName);
    }

    // --- GetReferencingForeignKeys ---

    [Fact]
    public void GetReferencingForeignKeys_ReferencedTable_ReturnsFks()
    {
        var provider = CreateErProvider();
        var table = provider.ResolveTable(null, null, "Users")!;
        var refs = provider.GetReferencingForeignKeys(table);

        Assert.Single(refs);
        Assert.Equal("FK_Orders_Users", refs[0].Name);
        Assert.Equal("Orders", refs[0].SourceTable.TableName);
    }

    [Fact]
    public void GetReferencingForeignKeys_MultipleReferences_ReturnsAll()
    {
        var provider = CreateErProvider();
        var table = provider.ResolveTable(null, null, "Orders")!;
        var refs = provider.GetReferencingForeignKeys(table);

        Assert.Single(refs);
        Assert.Equal("FK_OrderItems_Orders", refs[0].Name);
    }

    [Fact]
    public void GetReferencingForeignKeys_NotReferenced_ReturnsEmpty()
    {
        var provider = CreateErProvider();
        var table = provider.ResolveTable(null, null, "NoPkTable")!;
        var refs = provider.GetReferencingForeignKeys(table);

        Assert.Empty(refs);
    }

    [Fact]
    public void GetReferencingForeignKeys_TargetTableIsCorrect()
    {
        var provider = CreateErProvider();
        var table = provider.ResolveTable(null, null, "Users")!;
        var refs = provider.GetReferencingForeignKeys(table);

        Assert.Single(refs);
        Assert.Equal("Users", refs[0].TargetTable.TableName);
    }

    // --- IsUniqueColumnSet ---

    [Fact]
    public void IsUniqueColumnSet_PkColumn_ReturnsTrue()
    {
        var provider = CreateErProvider();
        var table = provider.ResolveTable(null, null, "Users")!;

        Assert.True(provider.IsUniqueColumnSet(table, ["Id"]));
    }

    [Fact]
    public void IsUniqueColumnSet_CompositePk_ReturnsTrue()
    {
        var provider = CreateErProvider();
        var table = provider.ResolveTable(null, null, "OrderItems")!;

        Assert.True(provider.IsUniqueColumnSet(table, ["OrderId", "ProductId"]));
    }

    [Fact]
    public void IsUniqueColumnSet_PartialCompositePk_ReturnsFalse()
    {
        var provider = CreateErProvider();
        var table = provider.ResolveTable(null, null, "OrderItems")!;

        Assert.False(provider.IsUniqueColumnSet(table, ["OrderId"]));
    }

    [Fact]
    public void IsUniqueColumnSet_UniqueConstraintColumn_ReturnsTrue()
    {
        var provider = CreateErProvider();
        var table = provider.ResolveTable(null, null, "Users")!;

        Assert.True(provider.IsUniqueColumnSet(table, ["Email"]));
    }

    [Fact]
    public void IsUniqueColumnSet_UniqueIndexColumn_ReturnsTrue()
    {
        var provider = CreateErProvider();
        var table = provider.ResolveTable(null, null, "Products")!;

        Assert.True(provider.IsUniqueColumnSet(table, ["Sku"]));
    }

    [Fact]
    public void IsUniqueColumnSet_NonUniqueColumn_ReturnsFalse()
    {
        var provider = CreateErProvider();
        var table = provider.ResolveTable(null, null, "Users")!;

        Assert.False(provider.IsUniqueColumnSet(table, ["Name"]));
    }

    [Fact]
    public void IsUniqueColumnSet_SupersetOfPk_ReturnsTrue()
    {
        var provider = CreateErProvider();
        var table = provider.ResolveTable(null, null, "Users")!;

        // PK is [Id], so [Id, Name] is a superset and still unique
        Assert.True(provider.IsUniqueColumnSet(table, ["Id", "Name"]));
    }

    [Fact]
    public void IsUniqueColumnSet_EmptyColumns_ReturnsFalse()
    {
        var provider = CreateErProvider();
        var table = provider.ResolveTable(null, null, "Users")!;

        Assert.False(provider.IsUniqueColumnSet(table, []));
    }

    [Fact]
    public void IsUniqueColumnSet_NoPk_NoConstraints_ReturnsFalse()
    {
        var provider = CreateErProvider();
        var table = provider.ResolveTable(null, null, "NoPkTable")!;

        Assert.False(provider.IsUniqueColumnSet(table, ["Col1"]));
    }

    [Fact]
    public void IsUniqueColumnSet_CaseInsensitive()
    {
        var provider = CreateErProvider();
        var table = provider.ResolveTable(null, null, "Users")!;

        Assert.True(provider.IsUniqueColumnSet(table, ["id"]));
        Assert.True(provider.IsUniqueColumnSet(table, ["ID"]));
    }

    // --- EstimateJoinCardinality ---

    [Fact]
    public void EstimateJoinCardinality_OneToOne_BothUnique()
    {
        var provider = CreateErProvider();
        var users = provider.ResolveTable(null, null, "Users")!;
        var orders = provider.ResolveTable(null, null, "Orders")!;

        // Users.Id (PK) joined with Orders.OrderId (PK) → 1:1
        var cardinality = provider.EstimateJoinCardinality(
            users, ["Id"], orders, ["OrderId"]);

        Assert.Equal(JoinCardinality.OneToOne, cardinality);
    }

    [Fact]
    public void EstimateJoinCardinality_OneToMany_LeftUnique()
    {
        var provider = CreateErProvider();
        var users = provider.ResolveTable(null, null, "Users")!;
        var orders = provider.ResolveTable(null, null, "Orders")!;

        // Users.Id (PK, unique) joined with Orders.UserId (non-unique) → 1:N
        var cardinality = provider.EstimateJoinCardinality(
            users, ["Id"], orders, ["UserId"]);

        Assert.Equal(JoinCardinality.OneToMany, cardinality);
    }

    [Fact]
    public void EstimateJoinCardinality_ManyToOne_RightUnique()
    {
        var provider = CreateErProvider();
        var orders = provider.ResolveTable(null, null, "Orders")!;
        var users = provider.ResolveTable(null, null, "Users")!;

        // Orders.UserId (non-unique) joined with Users.Id (PK) → N:1
        var cardinality = provider.EstimateJoinCardinality(
            orders, ["UserId"], users, ["Id"]);

        Assert.Equal(JoinCardinality.ManyToOne, cardinality);
    }

    [Fact]
    public void EstimateJoinCardinality_ManyToMany_NeitherUnique()
    {
        var provider = CreateErProvider();
        var orders = provider.ResolveTable(null, null, "Orders")!;
        var noPk = provider.ResolveTable(null, null, "NoPkTable")!;

        // Both non-unique → N:M
        var cardinality = provider.EstimateJoinCardinality(
            orders, ["UserId"], noPk, ["Col1"]);

        Assert.Equal(JoinCardinality.ManyToMany, cardinality);
    }

    [Fact]
    public void EstimateJoinCardinality_CompositeKey_OneToMany()
    {
        var provider = CreateErProvider();
        var orderItems = provider.ResolveTable(null, null, "OrderItems")!;
        var orders = provider.ResolveTable(null, null, "Orders")!;

        // OrderItems.OrderId (partial composite PK, non-unique) → Orders.OrderId (PK) → N:1
        var cardinality = provider.EstimateJoinCardinality(
            orderItems, ["OrderId"], orders, ["OrderId"]);

        Assert.Equal(JoinCardinality.ManyToOne, cardinality);
    }
}
