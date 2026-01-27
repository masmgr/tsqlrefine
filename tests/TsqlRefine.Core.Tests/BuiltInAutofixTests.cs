using TsqlRefine.Core.Engine;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Correctness;
using TsqlRefine.Rules.Rules.Style;

namespace TsqlRefine.Core.Tests;

public sealed class BuiltInAutofixTests
{
    [Fact]
    public void Fix_SemicolonTerminationRule_InsertsSemicolon()
    {
        var engine = new TsqlRefineEngine(new IRule[] { new SemicolonTerminationRule() });

        var result = engine.Fix(
            inputs: new[] { new SqlInput("<test>", "SELECT 1") },
            options: new EngineOptions()
        );

        Assert.Single(result.Files);
        Assert.Equal("SELECT 1;", result.Files[0].FixedText);
        Assert.Empty(result.Files[0].Diagnostics);
        Assert.Contains(result.Files[0].AppliedFixes, f => f.RuleId == "semicolon-termination");
    }

    [Fact]
    public void Fix_UnicodeStringRule_UpgradesVarcharToNvarchar()
    {
        var engine = new TsqlRefineEngine(new IRule[] { new UnicodeStringRule() });

        var result = engine.Fix(
            inputs: new[] { new SqlInput("<test>", "DECLARE @Name VARCHAR(50); SET @Name = N'あ';") },
            options: new EngineOptions()
        );

        Assert.Single(result.Files);
        Assert.Equal("DECLARE @Name NVARCHAR(50); SET @Name = N'あ';", result.Files[0].FixedText);
        Assert.Empty(result.Files[0].Diagnostics);
        Assert.Contains(result.Files[0].AppliedFixes, f => f.RuleId == "semantic/unicode-string");
    }
}

