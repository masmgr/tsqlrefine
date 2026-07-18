using TsqlRefine.Schema.Resolution;
using TsqlRefine.Schema.Tests.Helpers;

namespace TsqlRefine.Schema.Tests.Resolution;

public sealed class SchemaProviderConcurrencyTests
{
    [Fact]
    public void CachedMetadataAccess_IsThreadSafeAndConsistent()
    {
        var snapshot = TestSchemaBuilder.Create("TestDb")
            .AddTable("dbo", "Users", table => table
                .AddColumn("Id", "int")
                .AddColumn("Email", "nvarchar")
                .WithPrimaryKey(isClustered: true, "Id")
                .AddUniqueConstraint("UQ_Users_Email", "Email"))
            .AddTable("dbo", "Orders", table => table
                .AddColumn("Id", "int")
                .AddColumn("UserId", "int")
                .WithPrimaryKey(isClustered: true, "Id")
                .AddForeignKey("FK_Orders_Users", ["UserId"], "dbo", "Users", ["Id"]))
            .Build();
        var provider = new SchemaProvider(snapshot);
        var users = Assert.IsType<TsqlRefine.PluginSdk.ResolvedTable>(provider.ResolveTable(null, "dbo", "Users"));
        var orders = Assert.IsType<TsqlRefine.PluginSdk.ResolvedTable>(provider.ResolveTable(null, "dbo", "Orders"));
        var results = new string[500];

        Parallel.For(0, results.Length, index =>
        {
            results[index] = string.Join(
                ":",
                provider.GetUniqueConstraints(users).Count,
                provider.GetForeignKeys(orders).Count,
                provider.GetReferencingForeignKeys(users).Count,
                provider.IsUniqueColumnSet(users, ["Id"]),
                provider.IsUniqueColumnSet(users, ["Email"]));
        });

        Assert.Single(results.Distinct(StringComparer.Ordinal));
        Assert.Equal("1:1:1:True:True", results[0]);
    }
}
