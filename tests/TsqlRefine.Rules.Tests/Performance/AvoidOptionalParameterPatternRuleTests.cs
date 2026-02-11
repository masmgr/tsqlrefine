using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Performance;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Performance;

public sealed class AvoidOptionalParameterPatternRuleTests
{
    private readonly AvoidOptionalParameterPatternRule _rule = new();

    [Fact]
    public void Metadata_HasCorrectProperties()
    {
        Assert.Equal("avoid-optional-parameter-pattern", _rule.Metadata.RuleId);
        Assert.Equal("Performance", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Warning, _rule.Metadata.DefaultSeverity);
        Assert.False(_rule.Metadata.Fixable);
    }

    // === Pattern A: (@p IS NULL OR col = @p) ===

    [Fact]
    public void Analyze_IsNullOrEqualsPattern_ReturnsDiagnostic()
    {
        const string sql = "SELECT * FROM dbo.Users WHERE (@Name IS NULL OR Name = @Name);";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("avoid-optional-parameter-pattern", diagnostics[0].Code);
        Assert.Contains("Optional parameter pattern", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_ReversedIsNullOrEqualsPattern_ReturnsDiagnostic()
    {
        const string sql = "SELECT * FROM dbo.Users WHERE (Name = @Name OR @Name IS NULL);";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("avoid-optional-parameter-pattern", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_MultipleOptionalParameters_ReturnsMultipleDiagnostics()
    {
        const string sql = @"
            SELECT * FROM dbo.Users
            WHERE (@Name IS NULL OR Name = @Name)
              AND (@Status IS NULL OR Status = @Status);
        ";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Equal(2, diagnostics.Length);
        Assert.All(diagnostics, d => Assert.Equal("avoid-optional-parameter-pattern", d.Code));
    }

    [Fact]
    public void Analyze_OptionalParameterInProcedure_ReturnsDiagnostic()
    {
        const string sql = @"
            CREATE PROCEDURE dbo.SearchUsers
                @Name NVARCHAR(100) = NULL
            AS
            BEGIN
                SELECT * FROM dbo.Users
                WHERE (@Name IS NULL OR Name = @Name);
            END;
        ";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("avoid-optional-parameter-pattern", diagnostics[0].Code);
    }

    // === Pattern B: col = ISNULL(@p, col) ===

    [Fact]
    public void Analyze_IsnullPattern_ReturnsDiagnostic()
    {
        const string sql = "SELECT * FROM dbo.Users WHERE CustomerId = ISNULL(@CustId, CustomerId);";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("avoid-optional-parameter-pattern", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_IsnullPatternReversed_ReturnsDiagnostic()
    {
        const string sql = "SELECT * FROM dbo.Users WHERE ISNULL(@CustId, CustomerId) = CustomerId;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("avoid-optional-parameter-pattern", diagnostics[0].Code);
    }

    // === Negative cases ===

    [Fact]
    public void Analyze_IsNullOrDifferentValue_NoDiagnostic()
    {
        const string sql = "SELECT * FROM dbo.Users WHERE @Name IS NULL OR Name = 'Admin';";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_IsNullOrVariableComparison_NoDiagnostic()
    {
        const string sql = "SELECT * FROM dbo.Users WHERE @Name IS NULL OR @Other = @Name;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_IsnullWithLiteral_NoDiagnostic()
    {
        const string sql = "SELECT * FROM dbo.Users WHERE Status = ISNULL(@Status, 'Pending');";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_IsnullWithDifferentQualifiedColumn_NoDiagnostic()
    {
        const string sql = @"
            SELECT *
            FROM dbo.Users AS u
            INNER JOIN dbo.Orders AS o ON u.Id = o.UserId
            WHERE u.Id = ISNULL(@Id, o.Id);
        ";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_SimpleEquality_NoDiagnostic()
    {
        const string sql = "SELECT * FROM dbo.Users WHERE Name = @Name;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_IsNullCheck_NoDiagnostic()
    {
        const string sql = "SELECT * FROM dbo.Users WHERE Name IS NULL;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Theory]
    [InlineData("SELECT * FROM dbo.Users WHERE Name = @Name;")]
    [InlineData("SELECT * FROM dbo.Users WHERE Id > 0;")]
    [InlineData("")]
    public void Analyze_NonOptionalPattern_NoDiagnostic(string sql)
    {
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void GetFixes_ReturnsEmptyCollection()
    {
        const string sql = "SELECT * FROM dbo.Users WHERE (@Name IS NULL OR Name = @Name);";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostic = _rule.Analyze(context).First();

        var fixes = _rule.GetFixes(context, diagnostic);

        Assert.Empty(fixes);
    }
}
