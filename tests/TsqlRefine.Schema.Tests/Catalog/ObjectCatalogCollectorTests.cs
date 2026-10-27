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
    public void Collect_ResolvedOutOfScopeTableReference_SetsExternalReferenceFlag()
    {
        var catalog = ObjectCatalogCollector.Collect(
            [("CREATE VIEW dbo.UserView AS SELECT Id FROM dbo.Users;", "view.sql")],
            160);

        Assert.True(catalog.Scope.IncludesExternalReferences);
    }

    [Fact]
    public void Collect_CteAndTemporarySources_DoNotCreateTableReferences()
    {
        const string sql = """
            WITH recent AS (SELECT 1 AS Id)
            SELECT Id FROM recent;
            CREATE TABLE #items (Id int);
            SELECT Id FROM #items;
            DECLARE @items TABLE (Id int);
            SELECT Id FROM @items;
            """;

        var catalog = ObjectCatalogCollector.Collect([(sql, "transient.sql")], 160);

        Assert.DoesNotContain(catalog.References, reference =>
            reference.Kind == CatalogReferenceKind.Table &&
            reference.ToObject.Name is "recent" or "#items" or "@items");
    }

    [Fact]
    public void Collect_CorrelatedSubqueryUnqualifiedColumn_DoesNotGuessInnerSource()
    {
        const string sql = """
            CREATE VIEW dbo.CorrelatedView AS
            SELECT o.Id
            FROM dbo.OuterTable AS o
            WHERE EXISTS (SELECT 1 FROM dbo.InnerTable AS i WHERE UnqualifiedValue = o.Id);
            """;

        var catalog = ObjectCatalogCollector.Collect([(sql, "correlated.sql")], 160);

        Assert.DoesNotContain(catalog.References, reference =>
            reference.Kind == CatalogReferenceKind.Column &&
            reference.ToColumn == "UnqualifiedValue");
    }

    [Fact]
    public void Collect_SameSourceWithDifferentCasingAcrossScopes_ResolvesUnqualifiedColumn()
    {
        const string sql = """
            CREATE VIEW dbo.UserEmails AS
            SELECT outer_user.Id
            FROM dbo.Users AS outer_user
            WHERE EXISTS (
                SELECT 1
                FROM DBO.USERS AS inner_user
                WHERE Email = outer_user.Email);
            """;

        var catalog = ObjectCatalogCollector.Collect([(sql, "case.sql")], 160);

        var reference = Assert.Single(catalog.References, item =>
            item.Kind == CatalogReferenceKind.Column && item.ToColumn == "Email" &&
            item.ReferencedAt.Start is { Line: 6, Character: 10 });
        Assert.Equal("dbo", reference.ToObject.SchemaName, ignoreCase: true);
        Assert.Equal("Users", reference.ToObject.Name, ignoreCase: true);
    }

    [Fact]
    public void Collect_ParseErrors_ThrowsWithSourceLocation()
    {
        var exception = Assert.Throws<InvalidDataException>(() =>
            ObjectCatalogCollector.Collect([("CREATE PROCEDURE dbo.Bad AS SELECT * FROM", "bad.sql")], 160));

        Assert.Contains("bad.sql(", exception.Message, StringComparison.Ordinal);
        Assert.Contains("SQL", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Collect_KnownViewColumnIsValidatedDuringResolution()
    {
        var catalog = ObjectCatalogCollector.Collect(
            [
                ("CREATE VIEW dbo.SourceView (KnownColumn) AS SELECT 1;", "source.sql"),
                ("CREATE VIEW dbo.Consumer AS SELECT MissingColumn FROM dbo.SourceView;", "consumer.sql")
            ],
            160);

        var reference = Assert.Single(catalog.References, item =>
            item.Kind == CatalogReferenceKind.Column && item.ToColumn == "MissingColumn");
        Assert.Equal(CatalogResolutionStatus.Unresolved, reference.Resolution);
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

    [Fact]
    public void ResolveObject_MissingOrAmbiguous_ReturnsNull()
    {
        var catalog = ObjectCatalogCollector.Collect(
            [
                ("CREATE PROCEDURE dbo.Shared AS SELECT 1;", "procedure.sql"),
                ("CREATE VIEW dbo.Shared AS SELECT 1 AS Value;", "view.sql")
            ],
            160);
        var provider = new ObjectCatalogProvider(catalog);

        Assert.Null(provider.ResolveObject(null, "dbo", "Missing", CatalogObjectKindFilter.All));
        Assert.Null(provider.ResolveObject(null, "dbo", "Shared", CatalogObjectKindFilter.All));
        Assert.NotNull(provider.ResolveObject(null, "dbo", "Shared", CatalogObjectKindFilter.Procedure));
    }

    [Fact]
    public void Collect_MultilineIdentifierRangeEndsOnLastTokenLine()
    {
        const string sql = "CREATE PROCEDURE dbo.[Multi\nLine] AS SELECT 1;";

        var catalog = ObjectCatalogCollector.Collect([(sql, "multiline.sql")], 160);

        var definition = Assert.Single(catalog.Objects);
        Assert.Equal(1, definition.DefinedAt.End.Line);
    }
}
