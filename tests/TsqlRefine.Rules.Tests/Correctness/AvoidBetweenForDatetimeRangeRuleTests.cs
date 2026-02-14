using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Correctness;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Correctness;

public sealed class AvoidBetweenForDatetimeRangeRuleTests
{
    private readonly AvoidBetweenForDatetimeRangeRule _rule = new();

    [Fact]
    public void Metadata_HasCorrectProperties()
    {
        Assert.Equal("avoid-between-for-datetime-range", _rule.Metadata.RuleId);
        Assert.Equal("Correctness", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Warning, _rule.Metadata.DefaultSeverity);
        Assert.False(_rule.Metadata.Fixable);
    }

    // === Positive cases: column/variable name contains "time" ===

    [Fact]
    public void Analyze_BetweenWithCreatedTimeColumn_ReturnsDiagnostic()
    {
        const string sql = "SELECT * FROM dbo.Orders WHERE CreatedTime BETWEEN @from AND @to;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("avoid-between-for-datetime-range", diagnostics[0].Code);
        Assert.Contains("BETWEEN", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_BetweenWithDatetimeColumn_ReturnsDiagnostic()
    {
        const string sql = "SELECT * FROM dbo.Events WHERE EventDatetime BETWEEN '2024-01-01' AND '2024-12-31';";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("avoid-between-for-datetime-range", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_BetweenWithTimestampColumn_ReturnsDiagnostic()
    {
        const string sql = "SELECT * FROM dbo.Audit WHERE ModifiedTimestamp BETWEEN @start AND @end;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("avoid-between-for-datetime-range", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_BetweenWithTimeVariable_ReturnsDiagnostic()
    {
        const string sql = @"
            DECLARE @startTime DATETIME = '2024-01-01';
            DECLARE @endTime DATETIME = '2024-12-31';
            SELECT * FROM dbo.Orders WHERE OrderDate BETWEEN @startTime AND @endTime;
        ";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("avoid-between-for-datetime-range", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_BetweenWithQualifiedTimeColumn_ReturnsDiagnostic()
    {
        const string sql = "SELECT * FROM dbo.Orders AS o WHERE o.CreatedTime BETWEEN @from AND @to;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("avoid-between-for-datetime-range", diagnostics[0].Code);
    }

    // === Positive cases: datetime functions ===

    [Fact]
    public void Analyze_BetweenWithGetdate_ReturnsDiagnostic()
    {
        const string sql = "SELECT * FROM dbo.Orders WHERE OrderDate BETWEEN GETDATE() - 7 AND GETDATE();";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("avoid-between-for-datetime-range", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_BetweenWithSysdatetime_ReturnsDiagnostic()
    {
        const string sql = "SELECT * FROM dbo.Orders WHERE OrderDate BETWEEN SYSDATETIME() AND DATEADD(DAY, 7, SYSDATETIME());";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("avoid-between-for-datetime-range", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_BetweenWithGetutcdate_ReturnsDiagnostic()
    {
        const string sql = "SELECT * FROM dbo.Logs WHERE LogDate BETWEEN GETUTCDATE() - 1 AND GETUTCDATE();";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("avoid-between-for-datetime-range", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_BetweenWithCurrentTimestamp_ReturnsDiagnostic()
    {
        const string sql = "SELECT * FROM dbo.Events WHERE EventDate BETWEEN CURRENT_TIMESTAMP AND DATEADD(HOUR, 1, CURRENT_TIMESTAMP);";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("avoid-between-for-datetime-range", diagnostics[0].Code);
    }

    // === Positive cases: CAST/CONVERT to datetime types ===

    [Fact]
    public void Analyze_BetweenWithCastToDatetime_ReturnsDiagnostic()
    {
        const string sql = "SELECT * FROM dbo.Orders WHERE CAST(OrderDate AS DATETIME) BETWEEN @from AND @to;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("avoid-between-for-datetime-range", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_BetweenWithCastToDatetime2_ReturnsDiagnostic()
    {
        const string sql = "SELECT * FROM dbo.Orders WHERE CAST(OrderDate AS DATETIME2) BETWEEN @from AND @to;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("avoid-between-for-datetime-range", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_BetweenWithConvertToSmallDatetime_ReturnsDiagnostic()
    {
        const string sql = "SELECT * FROM dbo.Orders WHERE CONVERT(SMALLDATETIME, OrderDate) BETWEEN @from AND @to;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("avoid-between-for-datetime-range", diagnostics[0].Code);
    }

    // === Multiple diagnostics ===

    [Fact]
    public void Analyze_MultipleBetweenDatetime_ReturnsMultipleDiagnostics()
    {
        const string sql = @"
            SELECT * FROM dbo.Orders
            WHERE CreatedTime BETWEEN @from AND @to
              AND UpdatedTime BETWEEN @from2 AND @to2;
        ";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Equal(2, diagnostics.Length);
        Assert.All(diagnostics, d => Assert.Equal("avoid-between-for-datetime-range", d.Code));
    }

    // === Negative cases ===

    [Fact]
    public void Analyze_BetweenWithDateOnlyColumn_NoDiagnostic()
    {
        const string sql = "SELECT * FROM dbo.Orders WHERE OrderDate BETWEEN @from AND @to;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_BetweenWithNumericColumn_NoDiagnostic()
    {
        const string sql = "SELECT * FROM dbo.Products WHERE Price BETWEEN 10 AND 100;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_BetweenWithIdColumn_NoDiagnostic()
    {
        const string sql = "SELECT * FROM dbo.Users WHERE Id BETWEEN 1 AND 1000;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_GeAndLtPattern_NoDiagnostic()
    {
        const string sql = "SELECT * FROM dbo.Orders WHERE CreatedTime >= @from AND CreatedTime < @to;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_CastToInt_NoDiagnostic()
    {
        const string sql = "SELECT * FROM dbo.Orders WHERE CAST(Amount AS INT) BETWEEN 1 AND 100;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Theory]
    [InlineData("SELECT * FROM dbo.Orders WHERE Amount BETWEEN 10 AND 100;")]
    [InlineData("SELECT * FROM dbo.Orders WHERE Status BETWEEN 'A' AND 'Z';")]
    [InlineData("")]
    public void Analyze_NonDatetimeBetween_NoDiagnostic(string sql)
    {
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void GetFixes_ReturnsEmptyCollection()
    {
        const string sql = "SELECT * FROM dbo.Orders WHERE CreatedTime BETWEEN @from AND @to;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostic = _rule.Analyze(context).First();

        var fixes = _rule.GetFixes(context, diagnostic);

        Assert.Empty(fixes);
    }
}
