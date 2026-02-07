using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Security;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Security;

public sealed class AvoidDangerousProceduresRuleTests
{
    private readonly AvoidDangerousProceduresRule _rule = new();

    [Fact]
    public void Metadata_HasCorrectProperties()
    {
        Assert.Equal("avoid-dangerous-procedures", _rule.Metadata.RuleId);
        Assert.Equal("Security", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Warning, _rule.Metadata.DefaultSeverity);
        Assert.False(_rule.Metadata.Fixable);
    }

    [Theory]
    [InlineData("EXEC xp_cmdshell 'dir';", "xp_cmdshell")]
    [InlineData("EXECUTE xp_cmdshell 'dir';", "xp_cmdshell")]
    [InlineData("EXEC xp_regread @rootkey, @key, @value;", "xp_regread")]
    [InlineData("EXEC xp_regwrite @rootkey, @key, @valuename, @type, @value;", "xp_regwrite")]
    [InlineData("EXEC xp_regdeletekey @rootkey, @key;", "xp_regdeletekey")]
    [InlineData("EXEC xp_regdeletevalue @rootkey, @key, @valuename;", "xp_regdeletevalue")]
    [InlineData("EXEC xp_regaddmultistring @rootkey, @key, @valuename, @value;", "xp_regaddmultistring")]
    [InlineData("EXEC xp_regremovemultistring @rootkey, @key, @valuename, @value;", "xp_regremovemultistring")]
    [InlineData("EXEC sp_OACreate 'Scripting.FileSystemObject', @obj OUT;", "sp_OACreate")]
    [InlineData("EXEC sp_OAMethod @obj, 'CreateTextFile', @file OUT, @path;", "sp_OAMethod")]
    [InlineData("EXEC sp_OAGetProperty @obj, 'Value', @val OUT;", "sp_OAGetProperty")]
    [InlineData("EXEC sp_OASetProperty @obj, 'Value', @val;", "sp_OASetProperty")]
    [InlineData("EXEC sp_OADestroy @obj;", "sp_OADestroy")]
    [InlineData("EXEC sp_OAGetErrorInfo @obj;", "sp_OAGetErrorInfo")]
    public void Analyze_DangerousProcedure_ReturnsDiagnostic(string sql, string expectedProc)
    {
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("avoid-dangerous-procedures", diagnostics[0].Code);
        Assert.Contains(expectedProc, diagnostics[0].Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Analyze_XpCmdshellCaseInsensitive_ReturnsDiagnostic()
    {
        const string sql = "EXEC XP_CMDSHELL 'dir';";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("avoid-dangerous-procedures", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_SchemaQualified_ReturnsDiagnostic()
    {
        const string sql = "EXEC master.dbo.xp_cmdshell 'dir';";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("avoid-dangerous-procedures", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_MultipleDangerousCalls_ReturnsMultipleDiagnostics()
    {
        const string sql = @"
            EXEC xp_cmdshell 'dir';
            EXEC sp_OACreate 'Scripting.FileSystemObject', @obj OUT;
            EXEC xp_regread @rootkey, @key, @value;
        ";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Equal(3, diagnostics.Length);
        Assert.All(diagnostics, d => Assert.Equal("avoid-dangerous-procedures", d.Code));
    }

    [Theory]
    [InlineData("EXEC dbo.MyStoredProc @param = 1;")]
    [InlineData("EXEC sp_executesql @stmt;")]
    [InlineData("EXEC sp_help 'MyTable';")]
    [InlineData("EXEC sp_who;")]
    [InlineData("EXEC sp_addrolemember 'db_datareader', 'user1';")]
    [InlineData("EXEC(@dynamicSql);")]
    [InlineData("SELECT * FROM dbo.Users;")]
    [InlineData("")]
    public void Analyze_SafeProcedure_NoDiagnostic(string sql)
    {
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_MixedSafeAndDangerous_ReturnsOnlyDangerous()
    {
        const string sql = @"
            EXEC dbo.MyProc @id = 1;
            EXEC xp_cmdshell 'dir';
            EXEC sp_executesql @stmt;
        ";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Contains("xp_cmdshell", diagnostics[0].Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetFixes_ReturnsEmptyCollection()
    {
        const string sql = "EXEC xp_cmdshell 'dir';";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostic = _rule.Analyze(context).First();

        var fixes = _rule.GetFixes(context, diagnostic);

        Assert.Empty(fixes);
    }
}
