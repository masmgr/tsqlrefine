using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Correctness;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Correctness;

public sealed class TrimFromInReturnRuleTests
{
    private readonly TrimFromInReturnRule _rule = new();

    // --- Violation cases: TRIM with FROM clause in RETURN ---

    [Theory]
    [InlineData(@"
        CREATE FUNCTION dbo.TrimTest(@str NVARCHAR(MAX))
        RETURNS NVARCHAR(MAX)
        AS
        BEGIN
            RETURN TRIM('x' FROM @str);
        END")]
    [InlineData(@"
        CREATE FUNCTION dbo.TrimTest(@str NVARCHAR(MAX))
        RETURNS NVARCHAR(MAX)
        AS
        BEGIN
            RETURN TRIM(LEADING 'x' FROM @str);
        END")]
    [InlineData(@"
        CREATE FUNCTION dbo.TrimTest(@str NVARCHAR(MAX))
        RETURNS NVARCHAR(MAX)
        AS
        BEGIN
            RETURN TRIM(TRAILING 'x' FROM @str);
        END")]
    [InlineData(@"
        CREATE FUNCTION dbo.TrimTest(@str NVARCHAR(MAX))
        RETURNS NVARCHAR(MAX)
        AS
        BEGIN
            RETURN TRIM(BOTH 'x' FROM @str);
        END")]
    [InlineData(@"
        CREATE FUNCTION dbo.TrimTest(@str NVARCHAR(MAX))
        RETURNS NVARCHAR(MAX)
        AS
        BEGIN
            RETURN TRIM(NCHAR(12288) FROM @str);
        END")]
    [InlineData(@"
        CREATE FUNCTION dbo.TrimTest(@str NVARCHAR(MAX))
        RETURNS NVARCHAR(MAX)
        AS
        BEGIN
            RETURN TRIM(' ' FROM @str);
        END")]
    public void Analyze_TrimWithFromClauseInReturn_ReturnsDiagnostic(string sql)
    {
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("trim-from-in-return", diagnostics[0].Code);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostics[0].Severity);
    }

    // --- No violation: TRIM without FROM clause parses fine ---

    [Theory]
    [InlineData(@"
        CREATE FUNCTION dbo.TrimTest(@str NVARCHAR(MAX))
        RETURNS NVARCHAR(MAX)
        AS
        BEGIN
            RETURN TRIM(@str);
        END")]
    public void Analyze_TrimWithoutFromClause_ReturnsEmpty(string sql)
    {
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    // --- No violation: TRIM with FROM in SELECT/SET parses fine ---

    [Theory]
    [InlineData(@"SELECT TRIM('x' FROM col) FROM t;")]
    [InlineData(@"
        DECLARE @r NVARCHAR(MAX);
        SET @r = TRIM('x' FROM @str);")]
    [InlineData(@"
        CREATE FUNCTION dbo.TrimTest(@str NVARCHAR(MAX))
        RETURNS NVARCHAR(MAX)
        AS
        BEGIN
            DECLARE @result NVARCHAR(MAX);
            SET @result = TRIM('x' FROM @str);
            RETURN @result;
        END")]
    public void Analyze_TrimWithFromInSelectOrSet_ReturnsEmpty(string sql)
    {
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    // --- No violation: empty or unrelated SQL ---

    [Theory]
    [InlineData(@"")]
    [InlineData(@"SELECT * FROM users;")]
    [InlineData(@"SELECT id FROM t WHERE id = 1;")]
    public void Analyze_UnrelatedSql_ReturnsEmpty(string sql)
    {
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Metadata_HasCorrectProperties()
    {
        Assert.Equal("trim-from-in-return", _rule.Metadata.RuleId);
        Assert.Equal("Correctness", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Warning, _rule.Metadata.DefaultSeverity);
        Assert.False(_rule.Metadata.Fixable);
    }

    [Fact]
    public void GetFixes_ReturnsEmpty()
    {
        var context = RuleTestContext.CreateContext("SELECT 1;");
        var diagnostic = new Diagnostic(
            Range: new PluginSdk.Range(new Position(0, 0), new Position(0, 4)),
            Message: "test",
            Code: "trim-from-in-return"
        );

        var fixes = _rule.GetFixes(context, diagnostic).ToArray();

        Assert.Empty(fixes);
    }
}
