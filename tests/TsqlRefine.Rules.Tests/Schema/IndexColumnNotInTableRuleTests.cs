using TsqlRefine.Rules.Rules.Schema;
using TsqlRefine.Rules.Tests.Helpers;
using TsqlRefine.Schema.Resolution;
using TsqlRefine.Schema.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Schema;

public sealed class IndexColumnNotInTableRuleTests
{
    private readonly IndexColumnNotInTableRule _rule = new();

    private static SchemaProvider CreateSchema() =>
        new(TestSchemaBuilder.Create()
            .AddTable("dbo", "Users", t => t
                .AddColumn("Id", "int")
                .AddColumn("Name", "nvarchar", maxLength: 100)
                .AddColumn("Email", "nvarchar", maxLength: 200))
            .AddTable("dbo", "Orders", t => t
                .AddColumn("Id", "int")
                .AddColumn("UserId", "int")
                .AddColumn("Total", "decimal", precision: 18, scale: 2))
            .Build());

    // === CREATE INDEX: Positive cases ===

    [Fact]
    public void Analyze_CreateIndexBadKeyColumn_ReturnsDiagnostic()
    {
        const string sql = "CREATE INDEX IX_Name ON dbo.Users (BadCol);";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("index-column-not-in-table", diagnostics[0].Code);
        Assert.Contains("BadCol", diagnostics[0].Message);
        Assert.Contains("dbo.Users", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_CreateIndexBadIncludeColumn_ReturnsDiagnostic()
    {
        const string sql = "CREATE INDEX IX_Name ON dbo.Users (Id) INCLUDE (BadCol);";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Contains("BadCol", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_CreateIndexMultipleBadColumns_ReturnsMultipleDiagnostics()
    {
        const string sql = "CREATE INDEX IX_Name ON dbo.Users (Bad1, Bad2);";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Equal(2, diagnostics.Length);
    }

    [Fact]
    public void Analyze_CreateIndexMixedValidAndInvalid_ReportsOnlyInvalid()
    {
        const string sql = "CREATE INDEX IX_Name ON dbo.Users (Id, BadCol);";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Contains("BadCol", diagnostics[0].Message);
    }

    // === CREATE INDEX: Negative cases ===

    [Fact]
    public void Analyze_CreateIndexValidColumns_ReturnsEmpty()
    {
        const string sql = "CREATE INDEX IX_Name ON dbo.Users (Name);";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_CreateIndexValidInclude_ReturnsEmpty()
    {
        const string sql = "CREATE INDEX IX_Name ON dbo.Users (Id) INCLUDE (Name, Email);";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_CreateIndexTempTable_ReturnsEmpty()
    {
        const string sql = "CREATE INDEX IX_Name ON #Temp (Anything);";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_CreateIndexUnresolvedTable_ReturnsEmpty()
    {
        const string sql = "CREATE INDEX IX_Name ON dbo.NonExistent (Col1);";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_CreateIndexNoSchema_ReturnsEmpty()
    {
        var context = RuleTestContext.CreateContext("CREATE INDEX IX_Name ON dbo.Users (BadCol);");

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_CreateIndexCaseInsensitive_ReturnsEmpty()
    {
        const string sql = "CREATE INDEX IX_Name ON dbo.Users (ID, NAME);";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_CreateUniqueIndex_ValidatesColumns()
    {
        const string sql = "CREATE UNIQUE INDEX IX_Name ON dbo.Users (BadCol);";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Contains("BadCol", diagnostics[0].Message);
    }

    // === Inline index in CREATE TABLE: Positive cases ===

    [Fact]
    public void Analyze_InlineIndexBadColumn_ReturnsDiagnostic()
    {
        const string sql = "CREATE TABLE dbo.Foo (Id INT, INDEX IX_Name (BadCol));";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("index-column-not-in-table", diagnostics[0].Code);
        Assert.Contains("BadCol", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_InlineIndexBadInclude_ReturnsDiagnostic()
    {
        const string sql = "CREATE TABLE dbo.Foo (Id INT, INDEX IX_Name (Id) INCLUDE (BadCol));";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Contains("BadCol", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_MultipleInlineIndexesBadColumns_ReportsAll()
    {
        const string sql = "CREATE TABLE dbo.Foo (Id INT, INDEX IX1 (Bad1), INDEX IX2 (Bad2));";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Equal(2, diagnostics.Length);
    }

    // === Inline index in CREATE TABLE: Negative cases ===

    [Fact]
    public void Analyze_InlineIndexValidColumn_ReturnsEmpty()
    {
        const string sql = "CREATE TABLE dbo.Foo (Id INT, Name NVARCHAR(50), INDEX IX_Name (Name));";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_InlineIndexValidInclude_ReturnsEmpty()
    {
        const string sql = "CREATE TABLE dbo.Foo (Id INT, Name NVARCHAR(50), INDEX IX_Name (Id) INCLUDE (Name));";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_InlineIndexTempTable_ReturnsEmpty()
    {
        const string sql = "CREATE TABLE #Temp (Id INT, INDEX IX_Name (BadCol));";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_InlineIndexCaseInsensitive_ReturnsEmpty()
    {
        const string sql = "CREATE TABLE dbo.Foo (Id INT, INDEX IX_Name (ID));";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_InlineIndexNoSchemaNeeded_ValidatesWithoutSchema()
    {
        const string sql = "CREATE TABLE dbo.Foo (Id INT, INDEX IX_Name (BadCol));";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Contains("BadCol", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_CreateTableNoIndexes_ReturnsEmpty()
    {
        const string sql = "CREATE TABLE dbo.Foo (Id INT, Name NVARCHAR(50));";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }
}
