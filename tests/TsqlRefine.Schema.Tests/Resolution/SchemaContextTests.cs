using TsqlRefine.PluginSdk;
using TsqlRefine.Schema.Relations;
using TsqlRefine.Schema.Resolution;
using TsqlRefine.Schema.Tests.Helpers;

namespace TsqlRefine.Schema.Tests.Resolution;

public class SchemaContextTests
{
    private static SchemaProvider CreateProvider()
    {
        var snapshot = TestSchemaBuilder.Create("TestDb")
            .AddTable("dbo", "Users", t => t
                .AddColumn("Id", "int", isIdentity: true)
                .AddColumn("Name", "nvarchar", maxLength: 100))
            .AddTable("dbo", "Orders", t => t
                .AddColumn("OrderId", "int", isIdentity: true)
                .AddColumn("UserId", "int"))
            .Build();

        return new SchemaProvider(snapshot);
    }

    [Fact]
    public void SchemaContext_WithNullDeviations_HasRelationDeviationsIsFalse()
    {
        var provider = CreateProvider();
        ISchemaContext context = new SchemaContext(provider, relationDeviations: null);

        Assert.False(context.HasRelationDeviations);
        Assert.Null(context.RelationDeviations);
    }

    [Fact]
    public void SchemaContext_WithDeviations_HasRelationDeviationsIsTrue()
    {
        var provider = CreateProvider();
        var profile = new RelationProfile(
            new RelationProfileMetadata("2025-01-01T00:00:00Z", 0, 0, "hash"),
            []);
        var deviations = RelationDeviationProvider.FromProfile(profile);
        ISchemaContext context = new SchemaContext(provider, deviations);

        Assert.True(context.HasRelationDeviations);
        Assert.Same(deviations, context.RelationDeviations);
    }

    [Fact]
    public void SchemaContext_DelegatesResolveTable_ToUnderlyingProvider()
    {
        var provider = CreateProvider();
        var context = new SchemaContext(provider);

        var direct = provider.ResolveTable(null, "dbo", "Users");
        var viaContext = context.ResolveTable(null, "dbo", "Users");

        Assert.NotNull(direct);
        Assert.NotNull(viaContext);
        Assert.Equal(direct.TableName, viaContext.TableName);
        Assert.Equal(direct.SchemaName, viaContext.SchemaName);
    }

    [Fact]
    public void SchemaContext_DelegatesGetColumns_ToUnderlyingProvider()
    {
        var provider = CreateProvider();
        var context = new SchemaContext(provider);

        var table = context.ResolveTable(null, "dbo", "Users")!;
        var columnsFromProvider = provider.GetColumns(table);
        var columnsFromContext = context.GetColumns(table);

        Assert.Equal(columnsFromProvider.Count, columnsFromContext.Count);
        for (var i = 0; i < columnsFromProvider.Count; i++)
        {
            Assert.Equal(columnsFromProvider[i].Name, columnsFromContext[i].Name);
        }
    }

    [Fact]
    public void SchemaContext_DefaultSchema_MatchesProvider()
    {
        var provider = CreateProvider();
        var context = new SchemaContext(provider);

        Assert.Equal(provider.DefaultSchema, context.DefaultSchema);
    }

    [Fact]
    public void SchemaContext_Metadata_MatchesProvider()
    {
        var provider = CreateProvider();
        var context = new SchemaContext(provider);

        Assert.Equal(provider.Metadata.DatabaseName, context.Metadata.DatabaseName);
    }

    [Fact]
    public void SchemaContext_ImplementsISchemaProvider()
    {
        var provider = CreateProvider();
        ISchemaContext context = new SchemaContext(provider);

        Assert.IsAssignableFrom<ISchemaProvider>(context);
    }
}
