using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Correctness;
using TsqlRefine.Rules.Rules.Performance;
using TsqlRefine.Rules.Tests.Helpers;
using TsqlRefine.Schema.Catalog;

namespace TsqlRefine.Rules.Tests.Correctness;

public sealed class UnresolvedProcedureReferenceRuleTests
{
    [Fact]
    public void UnresolvedProcedure_AuthoritativeCatalog_ReturnsDiagnostic()
    {
        var context = CreateContext(
            "EXEC dbo.MissingProcedure;",
            "call.sql",
            [("CREATE PROCEDURE dbo.KnownProcedure AS SELECT 1;", "known.sql")]);

        var diagnostic = Assert.Single(new UnresolvedProcedureReferenceRule().Analyze(context));

        Assert.Equal("unresolved-procedure-reference", diagnostic.Code);
        Assert.Contains("dbo.MissingProcedure", diagnostic.Message);
    }

    [Fact]
    public void UnresolvedProcedure_NonAuthoritativeCatalog_ReturnsNoDiagnostic()
    {
        var catalog = ObjectCatalogCollector.Collect(
            [("CREATE PROCEDURE dbo.KnownProcedure AS SELECT 1;", "known.sql")],
            150,
            isAuthoritative: false);
        var context = RuleTestContext.CreateContext("EXEC dbo.MissingProcedure;") with
        {
            FilePath = "call.sql",
            ObjectCatalog = new ObjectCatalogProvider(catalog)
        };

        Assert.Empty(new UnresolvedProcedureReferenceRule().Analyze(context));
    }

    [Fact]
    public void UnresolvedProcedure_CrossDatabaseOutsideCatalogScope_ReturnsNoDiagnostic()
    {
        const string sql = "CREATE PROCEDURE dbo.Caller AS EXEC OtherDb.dbo.RemoteProcedure;";
        var context = CreateContext(sql, "caller.sql", [(sql, "caller.sql")]);

        Assert.Empty(new UnresolvedProcedureReferenceRule().Analyze(context));
    }

    [Fact]
    public void UnreferencedObject_NoIncomingReference_ReturnsDiagnostic()
    {
        const string definition = "CREATE PROCEDURE dbo.OrphanProcedure AS SELECT 1;";
        var context = CreateContext(definition, "orphan.sql", [(definition, "orphan.sql")]);

        var diagnostic = Assert.Single(new UnreferencedObjectRule().Analyze(context));

        Assert.Contains("dbo.OrphanProcedure", diagnostic.Message);
    }

    [Fact]
    public void UnreferencedObject_ConfiguredEntrypoint_ReturnsNoDiagnostic()
    {
        const string definition = "CREATE PROCEDURE dbo.OrphanProcedure AS SELECT 1;";
        var context = CreateContext(definition, "orphan.sql", [(definition, "orphan.sql")]) with
        {
            Settings = new RuleSettings(new TestOptions(text: "dbo.OrphanProcedure"))
        };

        Assert.Empty(new UnreferencedObjectRule().Analyze(context));
    }

    [Fact]
    public void DeepViewNesting_AboveConfiguredMaximum_ReturnsDiagnostic()
    {
        const string first = "CREATE VIEW dbo.FirstView AS SELECT Id FROM dbo.SecondView;";
        var inputs = new (string Sql, string FilePath)[]
        {
            (first, "first.sql"),
            ("CREATE VIEW dbo.SecondView AS SELECT Id FROM dbo.ThirdView;", "second.sql"),
            ("CREATE VIEW dbo.ThirdView AS SELECT Id FROM dbo.BaseTable;", "third.sql")
        };
        var context = CreateContext(first, "first.sql", inputs) with
        {
            Settings = new RuleSettings(new TestOptions(number: 1))
        };

        var diagnostic = Assert.Single(new DeepViewNestingRule().Analyze(context));

        Assert.Contains("nesting depth 2", diagnostic.Message);
    }

    [Fact]
    public void CircularObjectReference_Cycle_ReturnsDiagnostic()
    {
        const string first = "CREATE VIEW dbo.FirstView AS SELECT Id FROM dbo.SecondView;";
        var context = CreateContext(
            first,
            "first.sql",
            [
                (first, "first.sql"),
                ("CREATE VIEW dbo.SecondView AS SELECT Id FROM dbo.FirstView;", "second.sql")
            ]);

        var diagnostic = Assert.Single(new CircularObjectReferenceRule().Analyze(context));

        Assert.Contains("dbo.FirstView -> dbo.SecondView -> dbo.FirstView", diagnostic.Message);
    }

    [Fact]
    public void CircularObjectReference_RelativeAndAbsolutePaths_ReturnsDiagnostic()
    {
        const string first = "CREATE VIEW dbo.FirstView AS SELECT Id FROM dbo.SecondView;";
        var absolutePath = Path.GetFullPath("first.sql");
        var context = CreateContext(
            first,
            "first.sql",
            [
                (first, absolutePath),
                ("CREATE VIEW dbo.SecondView AS SELECT Id FROM dbo.FirstView;", "second.sql")
            ]);

        Assert.Single(new CircularObjectReferenceRule().Analyze(context));
    }

    [Fact]
    public void UnreferencedObject_InvalidCatalogPath_DoesNotThrow()
    {
        const string definition = "CREATE PROCEDURE dbo.OrphanProcedure AS SELECT 1;";
        var context = CreateContext(definition, "other.sql", [(definition, "\0")]);

        var exception = Record.Exception(() => new UnreferencedObjectRule().Analyze(context).ToArray());

        Assert.Null(exception);
    }

    [Fact]
    public void DependencyRules_NoCatalog_ReturnNoDiagnostics()
    {
        var context = RuleTestContext.CreateContext("EXEC dbo.MissingProcedure;");

        Assert.Empty(new UnresolvedProcedureReferenceRule().Analyze(context));
        Assert.Empty(new UnreferencedObjectRule().Analyze(context));
        Assert.Empty(new CircularObjectReferenceRule().Analyze(context));
        Assert.Empty(new DeepViewNestingRule().Analyze(context));
    }

    private static RuleContext CreateContext(
        string analyzedSql,
        string analyzedFile,
        IEnumerable<(string Sql, string FilePath)> catalogInputs)
    {
        var catalog = ObjectCatalogCollector.Collect(catalogInputs, 150);
        return RuleTestContext.CreateContext(analyzedSql) with
        {
            FilePath = analyzedFile,
            ObjectCatalog = new ObjectCatalogProvider(catalog)
        };
    }

    private sealed class TestOptions(int? number = null, string? text = null) : IRuleOptions
    {
        public bool TryGetBoolean(string name, out bool value)
        {
            value = default;
            return false;
        }

        public bool TryGetInt32(string name, out int value)
        {
            value = number.GetValueOrDefault();
            return name == "max" && number.HasValue;
        }

        public bool TryGetString(string name, out string? value)
        {
            value = text;
            return name == "entrypoints" && text is not null;
        }
    }
}

public sealed class UnreferencedObjectRuleTests
{
    [Fact]
    public void Metadata_UsesExpectedRuleId() =>
        Assert.Equal("unreferenced-object", new UnreferencedObjectRule().Metadata.RuleId);
}

public sealed class CircularObjectReferenceRuleTests
{
    [Fact]
    public void Metadata_UsesExpectedRuleId() =>
        Assert.Equal("circular-object-reference", new CircularObjectReferenceRule().Metadata.RuleId);
}

public sealed class DeepViewNestingRuleTests
{
    [Fact]
    public void Metadata_UsesExpectedRuleId() =>
        Assert.Equal("deep-view-nesting", new DeepViewNestingRule().Metadata.RuleId);
}
