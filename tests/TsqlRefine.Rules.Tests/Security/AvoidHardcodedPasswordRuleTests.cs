using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Security;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Security;

public sealed class AvoidHardcodedPasswordRuleTests
{
    private readonly AvoidHardcodedPasswordRule _rule = new();

    [Theory]
    [InlineData("CREATE LOGIN app_user WITH PASSWORD = 'secret';")]
    [InlineData("ALTER LOGIN app_user WITH PASSWORD = 'new-secret';")]
    [InlineData("ALTER LOGIN app_user WITH PASSWORD = 'new-secret' OLD_PASSWORD = 'old-secret';")]
    [InlineData("SELECT * FROM OPENROWSET('MSOLEDBSQL', 'server=db;uid=app;pwd=secret', 'SELECT 1') AS r;")]
    [InlineData("SELECT * FROM OPENDATASOURCE('MSOLEDBSQL', 'Server=db;User ID=app; Password = secret').db.dbo.t;")]
    public void Analyze_HardcodedPassword_ReturnsDiagnostic(string sql)
    {
        var diagnostics = _rule.Analyze(RuleTestContext.CreateContext(sql)).ToArray();

        Assert.NotEmpty(diagnostics);
        Assert.All(diagnostics, diagnostic =>
        {
            Assert.Equal("avoid-hardcoded-password", diagnostic.Code);
            Assert.Contains("hardcoded password", diagnostic.Message);
        });
    }

    [Theory]
    [InlineData("CREATE LOGIN domain_user FROM WINDOWS;")]
    [InlineData("CREATE LOGIN app_user WITH PASSWORD = 0x010203 HASHED;")]
    [InlineData("SELECT * FROM OPENROWSET('MSOLEDBSQL', 'Server=db;Trusted_Connection=yes', 'SELECT 1') AS r;")]
    [InlineData("")]
    public void Analyze_WithoutPlaintextPassword_ReturnsEmpty(string sql)
    {
        var diagnostics = _rule.Analyze(RuleTestContext.CreateContext(sql)).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Metadata_HasCorrectProperties()
    {
        Assert.Equal("Security", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Warning, _rule.Metadata.DefaultSeverity);
        Assert.False(_rule.Metadata.Fixable);
    }
}
