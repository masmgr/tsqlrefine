using TsqlRefine.Rules.Rules.Schema;
using TsqlRefine.Rules.Tests.Helpers;
using TsqlRefine.Schema.Relations;
using TsqlRefine.Schema.Resolution;
using TsqlRefine.Schema.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Schema;

public sealed class JoinColumnDeviationRuleTests
{
    private readonly JoinColumnDeviationRule _rule = new();

    // Schema: dbo.Orders (Id, UserId, CreatedBy, Amount), dbo.Users (Id, Name), dbo.Products (Id, Name)
    private static SchemaProvider CreateSchema() =>
        new(TestSchemaBuilder.Create()
            .AddTable("dbo", "Orders", t => t
                .AddColumn("Id", "int")
                .AddColumn("UserId", "int")
                .AddColumn("CreatedBy", "int")
                .AddColumn("Amount", "int")
                .WithPrimaryKey(true, "Id"))
            .AddTable("dbo", "Users", t => t
                .AddColumn("Id", "int")
                .AddColumn("Name", "nvarchar", maxLength: 100)
                .WithPrimaryKey(true, "Id"))
            .AddTable("dbo", "Products", t => t
                .AddColumn("Id", "int")
                .AddColumn("Name", "nvarchar", maxLength: 100)
                .WithPrimaryKey(true, "Id"))
            .Build());

    // Deviation data for dbo.Orders <-> dbo.Users (canonical order: Orders < Users)
    // Total: 133 occurrences, 5 patterns
    //   Dominant:   INNER JOIN on UserId=Id (90, ~68%)
    //   Common:     INNER JOIN on CreatedBy=Id (15, ~11%) — same join type, above rare threshold
    //   Rare:       INNER JOIN on Amount=Id (8, ~6%)
    //   VeryRare:   INNER JOIN on Amount=Id, CreatedBy=Id (3, ~2.3%)
    //   Structural: LEFT  JOIN on CreatedBy=Id (2, ~1.5%) — different join type
    //
    // Note: LEFT JOIN on UserId=Id (15 occurrences) is Structural because
    // DeviationAnalyzer classifies different-join-type patterns as Structural.
    // For the "Common" negative test we use INNER JOIN on CreatedBy=Id instead.
    private static RelationDeviationProvider CreateDeviations()
    {
        var relation = new TableRelation(
            "dbo", "Orders", "dbo", "Users",
            [
                new JoinPattern("INNER", [new ColumnPair("UserId", "Id")], 90, ["file1.sql"]),
                new JoinPattern("LEFT", [new ColumnPair("UserId", "Id")], 15, ["file2.sql"]),
                new JoinPattern("INNER", [new ColumnPair("CreatedBy", "Id")], 15, ["file3.sql"]),
                new JoinPattern("INNER", [new ColumnPair("Amount", "Id")], 8, ["file4.sql"]),
                new JoinPattern("INNER", [new ColumnPair("Amount", "Id"), new ColumnPair("CreatedBy", "Id")], 3, ["file5.sql"]),
                new JoinPattern("LEFT", [new ColumnPair("CreatedBy", "Id")], 2, ["file6.sql"]),
            ]);

        var profile = new RelationProfile(
            new RelationProfileMetadata("2026-01-01T00:00:00Z", 6, 133, "test"),
            [relation]);

        return RelationDeviationProvider.FromProfile(profile);
    }

    // ===== Positive cases: should detect =====

    [Fact]
    public void Analyze_RarePattern_ReturnsDiagnostic()
    {
        // INNER JOIN on Amount=Id is 8/133 (~6%), classified as Rare
        const string sql =
            "SELECT o.Id FROM dbo.Orders AS o INNER JOIN dbo.Users AS u ON u.Id = o.Amount;";
        var context = RuleTestContext.CreateContext(sql, CreateSchema(), CreateDeviations());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.NotEmpty(diagnostics);
        Assert.Contains(diagnostics, d => d.Code == "join-column-deviation");
        Assert.Contains(diagnostics, d => d.Message.Contains("rare"));
    }

    [Fact]
    public void Analyze_VeryRarePattern_ReturnsDiagnostic()
    {
        // INNER JOIN on Amount=Id AND CreatedBy=Id is 3/133 (~2.3%), classified as VeryRare
        const string sql =
            "SELECT o.Id FROM dbo.Orders AS o INNER JOIN dbo.Users AS u ON u.Id = o.Amount AND o.CreatedBy = u.Id;";
        var context = RuleTestContext.CreateContext(sql, CreateSchema(), CreateDeviations());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.NotEmpty(diagnostics);
        Assert.Contains(diagnostics, d => d.Code == "join-column-deviation");
    }

    [Fact]
    public void Analyze_StructuralPattern_ReturnsDiagnostic()
    {
        const string sql =
            "SELECT o.Id FROM dbo.Orders AS o LEFT JOIN dbo.Users AS u ON u.Id = o.CreatedBy;";
        var context = RuleTestContext.CreateContext(sql, CreateSchema(), CreateDeviations());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.NotEmpty(diagnostics);
        Assert.Contains(diagnostics, d => d.Code == "join-column-deviation");
    }

    [Fact]
    public void Analyze_UnseenPattern_ReturnsDiagnostic()
    {
        // RIGHT JOIN on UserId=Id is not in the profile
        const string sql =
            "SELECT o.Id FROM dbo.Orders AS o RIGHT JOIN dbo.Users AS u ON u.Id = o.UserId;";
        var context = RuleTestContext.CreateContext(sql, CreateSchema(), CreateDeviations());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.NotEmpty(diagnostics);
        Assert.Contains(diagnostics, d => d.Message.Contains("not observed"));
    }

    [Fact]
    public void Analyze_UnknownTablePair_ReturnsDiagnostic_WhenProviderHasData()
    {
        const string sql =
            "SELECT o.Id FROM dbo.Orders AS o INNER JOIN dbo.Products AS p ON p.Id = o.UserId;";
        var context = RuleTestContext.CreateContext(sql, CreateSchema(), CreateDeviations());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.NotEmpty(diagnostics);
        Assert.Contains(diagnostics, d => d.Message.Contains("not found in the relation profile"));
    }

    [Fact]
    public void Analyze_ReversedOnClauseOrder_StillDetects()
    {
        // ON o.Amount = u.Id (reversed compared to ON u.Id = o.Amount) — same Rare pattern
        const string sql =
            "SELECT o.Id FROM dbo.Orders AS o INNER JOIN dbo.Users AS u ON o.Amount = u.Id;";
        var context = RuleTestContext.CreateContext(sql, CreateSchema(), CreateDeviations());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.NotEmpty(diagnostics);
        Assert.Contains(diagnostics, d => d.Code == "join-column-deviation");
    }

    [Fact]
    public void Analyze_ReversedTableOrderWithLeftRightSwap_StillDetects()
    {
        // Users INNER JOIN Orders on Amount=Id
        // Canonical: Orders INNER JOIN Users on Amount=Id (tables swapped, INNER stays INNER)
        // Amount=Id is 8/133 (~6%), classified as Rare
        const string sql =
            "SELECT u.Name FROM dbo.Users AS u INNER JOIN dbo.Orders AS o ON o.Amount = u.Id;";
        var context = RuleTestContext.CreateContext(sql, CreateSchema(), CreateDeviations());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.NotEmpty(diagnostics);
        Assert.Contains(diagnostics, d => d.Code == "join-column-deviation");
    }

    [Fact]
    public void Analyze_CompositePairsDifferentOrder_StillDetects()
    {
        // VeryRare: INNER JOIN on Amount=Id AND CreatedBy=Id (3/133, ~2.3%)
        // Write them in reverse order in SQL: CreatedBy first, Amount second
        const string sql =
            "SELECT o.Id FROM dbo.Orders AS o INNER JOIN dbo.Users AS u ON o.CreatedBy = u.Id AND u.Id = o.Amount;";
        var context = RuleTestContext.CreateContext(sql, CreateSchema(), CreateDeviations());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.NotEmpty(diagnostics);
        Assert.Contains(diagnostics, d => d.Code == "join-column-deviation");
    }

    [Fact]
    public void Analyze_WithAlias_ReturnsDiagnostic()
    {
        const string sql =
            "SELECT a.Id FROM dbo.Orders a INNER JOIN dbo.Users b ON b.Id = a.Amount;";
        var context = RuleTestContext.CreateContext(sql, CreateSchema(), CreateDeviations());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.NotEmpty(diagnostics);
    }

    [Fact]
    public void Analyze_DiagnosticContainsDominantPatternAndStats()
    {
        const string sql =
            "SELECT o.Id FROM dbo.Orders AS o INNER JOIN dbo.Users AS u ON u.Id = o.Amount;";
        var context = RuleTestContext.CreateContext(sql, CreateSchema(), CreateDeviations());

        var diagnostics = _rule.Analyze(context).ToArray();

        var diag = Assert.Single(diagnostics);
        Assert.Contains("dominant pattern uses", diag.Message);
        Assert.Contains("UserId=Id", diag.Message);
    }

    // ===== Negative cases: should NOT detect =====

    [Fact]
    public void Analyze_DominantPattern_ReturnsEmpty()
    {
        const string sql =
            "SELECT o.Id FROM dbo.Orders AS o INNER JOIN dbo.Users AS u ON u.Id = o.UserId;";
        var context = RuleTestContext.CreateContext(sql, CreateSchema(), CreateDeviations());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_CommonPattern_ReturnsEmpty()
    {
        // INNER JOIN on CreatedBy=Id is 15/133 (~11%), classified as Common
        const string sql =
            "SELECT o.Id FROM dbo.Orders AS o INNER JOIN dbo.Users AS u ON u.Id = o.CreatedBy;";
        var context = RuleTestContext.CreateContext(sql, CreateSchema(), CreateDeviations());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_NoSchema_ReturnsEmpty()
    {
        const string sql =
            "SELECT o.Id FROM dbo.Orders AS o INNER JOIN dbo.Users AS u ON u.Id = o.Amount;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_NoDeviations_ReturnsEmpty()
    {
        const string sql =
            "SELECT o.Id FROM dbo.Orders AS o INNER JOIN dbo.Users AS u ON u.Id = o.Amount;";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_EmptyProfile_DoesNotReportUnknownTablePair()
    {
        const string sql =
            "SELECT o.Id FROM dbo.Orders AS o INNER JOIN dbo.Products AS p ON p.Id = o.UserId;";

        var emptyProfile = new RelationProfile(
            new RelationProfileMetadata("2026-01-01T00:00:00Z", 0, 0, "test"),
            []);
        var emptyProvider = new RelationDeviationProvider(
            new DeviationReport(new DeviationThresholds(), []));

        var context = RuleTestContext.CreateContext(sql, CreateSchema(), emptyProvider);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_TempTable_ReturnsEmpty()
    {
        const string sql =
            "SELECT t.Id FROM #Temp AS t INNER JOIN dbo.Users AS u ON u.Id = t.UserId;";
        var context = RuleTestContext.CreateContext(sql, CreateSchema(), CreateDeviations());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_DerivedTable_ReturnsEmpty()
    {
        const string sql =
            "SELECT o.Id FROM dbo.Orders AS o INNER JOIN (SELECT Id FROM dbo.Users) AS sub ON sub.Id = o.UserId;";
        var context = RuleTestContext.CreateContext(sql, CreateSchema(), CreateDeviations());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_CrossJoin_ReturnsEmpty()
    {
        const string sql =
            "SELECT o.Id FROM dbo.Orders AS o CROSS JOIN dbo.Users AS u;";
        var context = RuleTestContext.CreateContext(sql, CreateSchema(), CreateDeviations());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_NoFromClause_ReturnsEmpty()
    {
        const string sql = "SELECT 1;";
        var context = RuleTestContext.CreateContext(sql, CreateSchema(), CreateDeviations());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_UnresolvedTable_ReturnsEmpty()
    {
        const string sql =
            "SELECT o.Id FROM dbo.Orders AS o INNER JOIN dbo.NonExistent AS n ON n.Id = o.UserId;";
        var context = RuleTestContext.CreateContext(sql, CreateSchema(), CreateDeviations());

        var diagnostics = _rule.Analyze(context).ToArray();

        // NonExistent cannot be resolved by schema, so columns can't be resolved
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_AmbiguousPatternKey_ReturnsEmpty()
    {
        // Create a provider where the same JoinType + ColumnPairs matches multiple deviations
        // (different shape flags produce different entries in the profile, but the rule
        // only sees JoinType + ColumnPairDescriptions in the DTO)
        var relation = new TableRelation(
            "dbo", "Orders", "dbo", "Users",
            [
                new JoinPattern("INNER", [new ColumnPair("UserId", "Id")], 50, ["f1.sql"]),
                new JoinPattern("INNER", [new ColumnPair("UserId", "Id")], 10, ["f2.sql"], JoinShape.ContainsOr),
            ]);
        var profile = new RelationProfile(
            new RelationProfileMetadata("2026-01-01T00:00:00Z", 2, 60, "test"),
            [relation]);
        var ambiguousProvider = RelationDeviationProvider.FromProfile(profile);

        const string sql =
            "SELECT o.Id FROM dbo.Orders AS o INNER JOIN dbo.Users AS u ON u.Id = o.UserId;";
        var context = RuleTestContext.CreateContext(sql, CreateSchema(), ambiguousProvider);

        var diagnostics = _rule.Analyze(context).ToArray();

        // Ambiguous match: same JoinType + ColumnPairs but different shapes => skip
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_InsufficientData_ReturnsEmpty()
    {
        // Create a profile with very few occurrences (below MinTotal=10)
        var relation = new TableRelation(
            "dbo", "Orders", "dbo", "Users",
            [
                new JoinPattern("INNER", [new ColumnPair("UserId", "Id")], 3, ["f1.sql"]),
                new JoinPattern("INNER", [new ColumnPair("Amount", "Id")], 2, ["f2.sql"]),
            ]);
        var profile = new RelationProfile(
            new RelationProfileMetadata("2026-01-01T00:00:00Z", 2, 5, "test"),
            [relation]);
        var provider = RelationDeviationProvider.FromProfile(profile);

        const string sql =
            "SELECT o.Id FROM dbo.Orders AS o INNER JOIN dbo.Users AS u ON u.Id = o.Amount;";
        var context = RuleTestContext.CreateContext(sql, CreateSchema(), provider);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }
}
