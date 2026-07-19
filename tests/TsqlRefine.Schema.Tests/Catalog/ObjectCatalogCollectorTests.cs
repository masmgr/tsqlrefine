using TsqlRefine.PluginSdk;
using TsqlRefine.Schema.Catalog;

namespace TsqlRefine.Schema.Tests.Catalog;

public sealed class ObjectCatalogCollectorTests
{
    [Fact]
    public void Collect_ProceduresAndExec_CollectsSignatureAndResolvedReference()
    {
        const string definition = """
            CREATE PROCEDURE dbo.SaveUser
                @id int,
                @name nvarchar(50) = N'unknown',
                @result int OUTPUT
            AS SELECT @result = @id;
            """;
        const string caller = """
            CREATE PROCEDURE dbo.RunSave AS
                DECLARE @result int;
                EXEC dbo.SaveUser 1, @result = @result OUTPUT;
            """;

        var catalog = ObjectCatalogCollector.Collect(
            [(definition, "save-user.sql"), (caller, "run-save.sql")], 160);

        Assert.Equal(2, catalog.Objects.Count);
        var target = Assert.Single(catalog.Objects, obj => obj.Id.Name == "SaveUser");
        Assert.Equal(CatalogObjectKind.Procedure, target.Kind);
        Assert.Collection(
            target.Parameters,
            parameter => Assert.Equal("@id", parameter.Name),
            parameter => Assert.True(parameter.HasDefault),
            parameter => Assert.True(parameter.IsOutput));
        Assert.Equal("nvarchar", target.Parameters[1].Type.TypeName);

        var reference = Assert.Single(catalog.References, item => item.Kind == CatalogReferenceKind.Execute);
        Assert.Equal(CatalogResolutionStatus.Resolved, reference.Resolution);
        Assert.Equal("RunSave", reference.FromObject?.Name);
        Assert.Equal("SaveUser", reference.ToObject.Name);
    }

    [Fact]
    public void Collect_FunctionAndView_CollectsKindsAndReferences()
    {
        const string function = """
            CREATE FUNCTION util.DoubleValue(@value int) RETURNS int
            AS BEGIN RETURN @value * 2; END;
            """;
        const string viewSql = """
            CREATE VIEW reporting.[Values] (Doubled) AS
                SELECT [util].[DoubleValue](v.[Value]) FROM dbo.[Values] AS v;
            """;

        var catalog = ObjectCatalogCollector.Collect(
            [(function, "function.sql"), (viewSql, "view.sql")], 160);

        Assert.Contains(catalog.Objects, obj => obj.Kind == CatalogObjectKind.ScalarFunction);
        var view = Assert.Single(catalog.Objects, obj => obj.Kind == CatalogObjectKind.View);
        Assert.Equal("reporting", view.Id.SchemaName);
        Assert.Equal("Doubled", Assert.Single(view.ResultColumns!).Name);
        Assert.Contains(
            catalog.References,
            reference => reference.Kind == CatalogReferenceKind.FunctionCall &&
                         reference.Resolution == CatalogResolutionStatus.Resolved);
    }

    [Fact]
    public void Collect_AliasAndUnqualifiedColumns_ResolveToTheirSingleTableSource()
    {
        var catalog = ObjectCatalogCollector.Collect(
            [
                ("CREATE VIEW dbo.AliasView AS SELECT u.Email FROM dbo.Users AS u;", "alias.sql"),
                ("CREATE VIEW dbo.UnqualifiedView AS SELECT Email FROM dbo.Users;", "unqualified.sql")
            ],
            160);

        var columnReferences = catalog.References
            .Where(reference => reference.Kind == CatalogReferenceKind.Column)
            .ToArray();
        Assert.Equal(2, columnReferences.Length);
        Assert.All(columnReferences, reference =>
        {
            Assert.Equal("dbo", reference.ToObject.SchemaName);
            Assert.Equal("Users", reference.ToObject.Name);
            Assert.Equal("Email", reference.ToColumn);
        });
    }

    [Fact]
    public void Collect_UpdateColumn_ResolvesToTargetTable()
    {
        var catalog = ObjectCatalogCollector.Collect(
            [("CREATE PROCEDURE dbo.UpdateUser AS UPDATE dbo.Users SET Email = N'new@example.com';", "update.sql")],
            160);

        var reference = Assert.Single(
            catalog.References,
            item => item.Kind == CatalogReferenceKind.Column && item.ToColumn == "Email");
        Assert.Equal("dbo", reference.ToObject.SchemaName);
        Assert.Equal("Users", reference.ToObject.Name);
    }

    [Fact]
    public void Collect_CrossDatabaseReference_DoesNotExpandAuthoritativeDatabaseScope()
    {
        var catalog = ObjectCatalogCollector.Collect(
            [("CREATE PROCEDURE dbo.Caller AS EXEC OtherDb.dbo.RemoteProcedure;", "caller.sql")],
            160);

        Assert.DoesNotContain("OtherDb", catalog.Scope.Databases, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Collect_DynamicAndFourPartExec_MarksReferencesOutOfScope()
    {
        var catalog = ObjectCatalogCollector.Collect(
            [
                ("DECLARE @procedure sysname = N'dbo.P'; EXEC @procedure;", "dynamic.sql"),
                ("EXEC [linked].[database].[dbo].[RemoteProcedure];", "external.sql")
            ],
            160);

        Assert.Equal(2, catalog.References.Count);
        Assert.All(catalog.References, reference =>
            Assert.Equal(CatalogResolutionStatus.OutOfScope, reference.Resolution));
        Assert.Contains(catalog.References, reference => reference.IsDynamic);
        Assert.True(catalog.Scope.IncludesExternalReferences);
    }

    [Fact]
    public void Serialize_RoundTripsCatalogAndProviderResolvesCaseInsensitively()
    {
        const string sql = "CREATE PROCEDURE Sales.ProcessOrder @id bigint AS SELECT @id;";
        var original = ObjectCatalogCollector.Collect([(sql, "order.sql")], 160);

        var restored = ObjectCatalogSerializer.Deserialize(ObjectCatalogSerializer.Serialize(original));
        var provider = new ObjectCatalogProvider(restored);
        var resolved = provider.ResolveObject(null, "SALES", "processorder", CatalogObjectKindFilter.Procedure);

        Assert.NotNull(resolved);
        Assert.True(provider.HasData);
        Assert.Equal("@id", Assert.Single(resolved.Parameters).Name);
    }
}
