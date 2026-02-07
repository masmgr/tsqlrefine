using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Security;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Security;

public sealed class AvoidOpenrowsetOpendatasourceRuleTests
{
    private readonly AvoidOpenrowsetOpendatasourceRule _rule = new();

    [Fact]
    public void Metadata_HasCorrectProperties()
    {
        Assert.Equal("avoid-openrowset-opendatasource", _rule.Metadata.RuleId);
        Assert.Equal("Security", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Warning, _rule.Metadata.DefaultSeverity);
        Assert.False(_rule.Metadata.Fixable);
    }

    [Fact]
    public void Analyze_OpenRowsetBulk_ReturnsDiagnostic()
    {
        const string sql = """
            SELECT *
            FROM OPENROWSET(BULK 'C:\\data\\file.csv',
                FORMATFILE = 'C:\\data\\format.fmt') AS t;
            """;
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("avoid-openrowset-opendatasource", diagnostics[0].Code);
        Assert.Contains("OPENROWSET", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_OpenRowsetWithProvider_ReturnsDiagnostic()
    {
        const string sql = """
            SELECT *
            FROM OPENROWSET('SQLNCLI', 'Server=remote;Trusted_Connection=yes;',
                'SELECT * FROM dbo.Users') AS t;
            """;
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("avoid-openrowset-opendatasource", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_Opendatasource_ReturnsDiagnostic()
    {
        const string sql = """
            SELECT *
            FROM OPENDATASOURCE('SQLNCLI', 'Data Source=remote;Integrated Security=SSPI').AdventureWorks.dbo.Users;
            """;
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("avoid-openrowset-opendatasource", diagnostics[0].Code);
        Assert.Contains("OPENDATASOURCE", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_MultipleOpenRowsetBulk_ReturnsMultipleDiagnostics()
    {
        const string sql = """
            SELECT *
            FROM OPENROWSET(BULK 'C:\\data\\file1.csv',
                FORMATFILE = 'C:\\data\\format.fmt') AS t1
            CROSS JOIN OPENROWSET(BULK 'C:\\data\\file2.csv',
                FORMATFILE = 'C:\\data\\format.fmt') AS t2;
            """;
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Equal(2, diagnostics.Length);
        Assert.All(diagnostics, d => Assert.Equal("avoid-openrowset-opendatasource", d.Code));
    }

    [Theory]
    [InlineData("SELECT * FROM dbo.Users;")]
    [InlineData("EXEC dbo.MyProc;")]
    [InlineData("SELECT * FROM OPENJSON(@json) AS j;")]
    [InlineData("")]
    public void Analyze_SafeQuery_NoDiagnostic(string sql)
    {
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void GetFixes_ReturnsEmptyCollection()
    {
        const string sql = """
            SELECT *
            FROM OPENROWSET('SQLNCLI', 'Server=remote;Trusted_Connection=yes;',
                'SELECT * FROM dbo.Users') AS t;
            """;
        var context = RuleTestContext.CreateContext(sql);
        var diagnostic = _rule.Analyze(context).First();

        var fixes = _rule.GetFixes(context, diagnostic);

        Assert.Empty(fixes);
    }
}
