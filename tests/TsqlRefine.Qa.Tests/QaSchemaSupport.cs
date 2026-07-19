using TsqlRefine.PluginSdk;
using TsqlRefine.Schema.Model;
using TsqlRefine.Schema.Relations;
using TsqlRefine.Schema.Resolution;
using TsqlRefine.Schema.Snapshot;
using TsqlRefine.Schema.TypeSystem;

namespace TsqlRefine.Qa.Tests;

/// <summary>
/// Provides schema and relation data that exercises schema-dependent built-in rules
/// against their committed sample SQL files.
/// </summary>
internal static class QaSchemaSupport
{
    public static ISchemaContext CreateContext()
    {
        var tables = new TableSchema[]
        {
            Table("Users",
                [Column("Id", "int"), Column("Name", "nvarchar", 100), Column("Email", "varchar", 200)],
                primaryKey: "Id"),
            Table("Orders",
                [
                    Column("OrderId", "int"), Column("Id", "int"), Column("UserId", "int"),
                    Column("CreatedBy", "int"), Column("ProductId", "int"), Column("CustomerId", "int"),
                    Column("Amount", "decimal", precision: 18, scale: 2), Column("Total", "decimal", precision: 18, scale: 2),
                    Column("Status", "varchar", 50)
                ],
                primaryKey: "OrderId"),
            Table("OrderItems",
                [Column("OrderId", "int"), Column("Quantity", "int")]),
            Table("OrderLog",
                [Column("OrderId", "int"), Column("Action", "varchar", 50)]),
            Table("Customers",
                [Column("CustomerId", "int"), Column("Name", "varchar", 100)],
                primaryKey: "CustomerId"),
            Table("OrderSummary",
                [Column("OrderId", "int"), Column("TotalAmount", "decimal", precision: 18, scale: 2)],
                primaryKey: "OrderId"),
            Table("Products",
                [Column("Id", "int"), Column("Name", "nvarchar", 100)],
                primaryKey: "Id"),
            Table("TableA",
                [Column("Id", "int"), Column("ID_B", "int"), Column("Name", "nvarchar", 100)],
                primaryKey: "Id",
                foreignKeys:
                [
                    new ForeignKeyInfo("FK_TableA_TableB", ["ID_B"], "dbo", "TableB", ["Id"])
                ]),
            Table("TableB",
                [Column("Id", "int"), Column("Value", "nvarchar", 200)],
                primaryKey: "Id"),
            Table("TableC",
                [Column("Id", "int"), Column("Value", "nvarchar", 200)],
                primaryKey: "Id")
        };

        var snapshot = new SchemaSnapshot(
            new SnapshotMetadata("2026-01-01T00:00:00Z", "qa", "QaDb", 160, "qa"),
            [new DatabaseSchema("QaDb", tables, [])]);
        var schema = new SchemaProvider(snapshot);

        var relation = new TableRelation(
            "dbo", "Orders", "dbo", "Users",
            [
                new JoinPattern("INNER", [new ColumnPair("UserId", "Id")], 90, ["dominant.sql"]),
                new JoinPattern("INNER", [new ColumnPair("CreatedBy", "Id")], 5, ["rare.sql"]),
                new JoinPattern("INNER", [new ColumnPair("Amount", "Id")], 3, ["very-rare.sql"]),
                new JoinPattern(
                    "INNER",
                    [new ColumnPair("Amount", "Id"), new ColumnPair("CreatedBy", "Id")],
                    2,
                    ["composite.sql"])
            ]);
        var profile = new RelationProfile(
            new RelationProfileMetadata("2026-01-01T00:00:00Z", 4, 100, "qa"),
            [relation]);

        return new SchemaContext(schema, RelationDeviationProvider.FromProfile(profile));
    }

    private static TableSchema Table(
        string name,
        IReadOnlyList<ColumnSchema> columns,
        string? primaryKey = null,
        IReadOnlyList<ForeignKeyInfo>? foreignKeys = null) =>
        new(
            "dbo",
            name,
            columns,
            primaryKey is null ? null : new PrimaryKeyInfo([primaryKey], IsClustered: true),
            ForeignKeys: foreignKeys);

    private static ColumnSchema Column(
        string name,
        string typeName,
        int? maxLength = null,
        int? precision = null,
        int? scale = null) =>
        new(
            name,
            new SqlTypeInfo(typeName, TypeCategoryMapper.FromTypeName(typeName), maxLength, precision, scale),
            IsNullable: false);
}
