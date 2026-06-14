using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Correctness;
using TsqlRefine.Rules.Tests.Helpers;
using TsqlRefine.Schema.Resolution;
using TsqlRefine.Schema.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Correctness;

public sealed class UnionTypeMismatchRuleTests
{
    private readonly UnionTypeMismatchRule _rule = new();

    private static SchemaProvider CreateSchema() =>
        new(TestSchemaBuilder.Create()
            .AddTable("dbo", "Users", t => t
                .AddColumn("Id", "int")
                .AddColumn("Name", "nvarchar", maxLength: 100)
                .AddColumn("Email", "varchar", maxLength: 200)
                .AddColumn("CreatedAt", "datetime2")
                .AddColumn("ExternalId", "uniqueidentifier"))
            .AddTable("dbo", "Products", t => t
                .AddColumn("Id", "int")
                .AddColumn("Title", "nvarchar", maxLength: 200)
                .AddColumn("Price", "decimal", precision: 10, scale: 2))
            .Build());

    [Fact]
    public void Metadata_HasCorrectProperties()
    {
        Assert.Equal("union-type-mismatch", _rule.Metadata.RuleId);
        Assert.Equal("Correctness", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Error, _rule.Metadata.DefaultSeverity);
        Assert.False(_rule.Metadata.Fixable);
    }

    [Fact]
    public void Analyze_NumericVsString_ReturnsDiagnostic()
    {
        const string sql = "SELECT 1 AS Id UNION ALL SELECT 'a' AS Id;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("union-type-mismatch", diagnostics[0].Code);
        Assert.Contains("numeric", diagnostics[0].Message);
        Assert.Contains("string", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_StringVsNumeric_ReturnsDiagnostic()
    {
        const string sql = "SELECT 'hello' AS Val UNION SELECT 42 AS Val;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("union-type-mismatch", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_MultipleColumnMismatches_ReturnsMultipleDiagnostics()
    {
        const string sql = "SELECT 1, 'text' UNION ALL SELECT 'a', 2;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        // Each mismatched column generates a separate diagnostic (on the same BinaryQueryExpression)
        Assert.Equal(2, diagnostics.Length);
        Assert.All(diagnostics, d => Assert.Equal("union-type-mismatch", d.Code));
    }

    [Fact]
    public void Analyze_CastMismatch_ReturnsDiagnostic()
    {
        const string sql = "SELECT CAST(1 AS INT) UNION ALL SELECT CAST('a' AS VARCHAR(10));";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("union-type-mismatch", diagnostics[0].Code);
        Assert.Contains("numeric", diagnostics[0].Message);
        Assert.Contains("string", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_SameTypes_NoDiagnostic()
    {
        const string sql = "SELECT 1 AS Id UNION ALL SELECT 2 AS Id;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_SameStringTypes_NoDiagnostic()
    {
        const string sql = "SELECT 'hello' AS Val UNION ALL SELECT 'world' AS Val;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_ColumnReferences_NoDiagnostic()
    {
        // Can't determine types from column references
        const string sql = "SELECT Name FROM Users UNION ALL SELECT Title FROM Products;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_NullLiteral_NoDiagnostic()
    {
        // NULL is compatible with any type
        const string sql = "SELECT 1 AS Id UNION ALL SELECT NULL AS Id;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_UnionAll_ReturnsDiagnostic()
    {
        const string sql = "SELECT 1 UNION ALL SELECT 'a';";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
    }

    [Fact]
    public void Analyze_ParenthesizedLeftQuery_ReturnsDiagnostic()
    {
        const string sql = "(SELECT 1) UNION ALL SELECT 'a';";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("union-type-mismatch", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_ParenthesizedRightQuery_ReturnsDiagnostic()
    {
        const string sql = "SELECT 1 UNION ALL (SELECT 'a');";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("union-type-mismatch", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_TryCastTypeMismatch_ReturnsDiagnostic()
    {
        const string sql = "SELECT TRY_CAST(1 AS INT) UNION ALL SELECT TRY_CAST('a' AS VARCHAR(10));";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("union-type-mismatch", diagnostics[0].Code);
        Assert.Contains("numeric", diagnostics[0].Message);
        Assert.Contains("string", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_Except_NoDiagnostic()
    {
        // Only UNION is checked
        const string sql = "SELECT 1 EXCEPT SELECT 'a';";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_ThreeWayUnion_ReturnsDiagnostics()
    {
        const string sql = "SELECT 1 UNION ALL SELECT 'a' UNION ALL SELECT 2;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        // The first UNION (1 vs 'a') generates a diagnostic
        Assert.True(diagnostics.Length >= 1);
        Assert.All(diagnostics, d => Assert.Equal("union-type-mismatch", d.Code));
    }

    [Theory]
    [InlineData("SELECT * FROM Users;")]
    [InlineData("")]
    public void Analyze_NoUnion_NoDiagnostic(string sql)
    {
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void GetFixes_ReturnsEmptyCollection()
    {
        const string sql = "SELECT 1 AS Id UNION ALL SELECT 'a' AS Id;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostic = _rule.Analyze(context).First();

        var fixes = _rule.GetFixes(context, diagnostic);

        Assert.Empty(fixes);
    }

    // --- Schema-aware tests ---

    [Fact]
    public void Analyze_ColumnRefIntVsNvarchar_WithSchema_ReturnsDiagnostic()
    {
        const string sql = "SELECT Id FROM dbo.Users UNION ALL SELECT Name FROM dbo.Users;";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Contains("numeric", diagnostics[0].Message);
        Assert.Contains("string", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_ColumnRefIntVsInt_WithSchema_NoDiagnostic()
    {
        const string sql = "SELECT Id FROM dbo.Users UNION ALL SELECT Id FROM dbo.Products;";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_ColumnRefVsLiteral_TypeMismatch_WithSchema_ReturnsDiagnostic()
    {
        const string sql = "SELECT Name FROM dbo.Users UNION ALL SELECT 42;";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Contains("string", diagnostics[0].Message);
        Assert.Contains("numeric", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_ColumnRefVsLiteral_Compatible_WithSchema_NoDiagnostic()
    {
        const string sql = "SELECT Id FROM dbo.Users UNION ALL SELECT 42;";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_VarcharVsNvarchar_SameCategory_WithSchema_NoDiagnostic()
    {
        const string sql = "SELECT Email FROM dbo.Users UNION ALL SELECT Title FROM dbo.Products;";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_AliasedColumnRef_WithSchema_ReturnsDiagnostic()
    {
        const string sql = "SELECT u.Id FROM dbo.Users u UNION ALL SELECT p.Title FROM dbo.Products p;";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Contains("numeric", diagnostics[0].Message);
        Assert.Contains("string", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_ColumnRefs_NoSchema_NoDiagnostic()
    {
        // Without schema, column references are still unresolvable
        const string sql = "SELECT Id FROM dbo.Users UNION ALL SELECT Name FROM dbo.Users;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_UnresolvableTable_WithSchema_NoDiagnostic()
    {
        const string sql = "SELECT Id FROM dbo.UnknownTable UNION ALL SELECT 'text';";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_SelectStar_WithSchema_NoDiagnostic()
    {
        const string sql = "SELECT * FROM dbo.Users UNION ALL SELECT * FROM dbo.Products;";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_DatetimeVsString_WithSchema_ReturnsDiagnostic()
    {
        const string sql = "SELECT CreatedAt FROM dbo.Users UNION ALL SELECT Title FROM dbo.Products;";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Contains("datetime", diagnostics[0].Message);
        Assert.Contains("string", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_UniqueidentifierVsString_WithSchema_ReturnsDiagnostic()
    {
        const string sql = "SELECT ExternalId FROM dbo.Users UNION ALL SELECT Title FROM dbo.Products;";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Contains("uniqueidentifier", diagnostics[0].Message);
        Assert.Contains("string", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_NoFromClause_WithSchema_LiteralDetectionStillWorks()
    {
        const string sql = "SELECT 1 UNION ALL SELECT 'text';";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
    }

    [Fact]
    public void Analyze_ColumnRefIntVsCastVarchar_WithSchema_ReturnsDiagnostic()
    {
        const string sql = "SELECT Id FROM dbo.Users UNION ALL SELECT CAST('x' AS VARCHAR(10));";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Contains("numeric", diagnostics[0].Message);
        Assert.Contains("string", diagnostics[0].Message);
    }
}
