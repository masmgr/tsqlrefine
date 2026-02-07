using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Performance;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Performance;

public sealed class DisallowCursorsRuleTests
{
    [Fact]
    public void Metadata_ShouldHaveCorrectProperties()
    {
        var rule = new DisallowCursorsRule();

        Assert.Equal("disallow-cursors", rule.Metadata.RuleId);
        Assert.Equal("Performance", rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Warning, rule.Metadata.DefaultSeverity);
        Assert.False(rule.Metadata.Fixable);
    }

    [Theory]
    [InlineData("DECLARE cursor_name CURSOR FOR SELECT * FROM users;")]
    [InlineData(@"
        DECLARE @name VARCHAR(50);
        DECLARE cursor_name CURSOR FOR SELECT name FROM users;
        OPEN cursor_name;
        FETCH NEXT FROM cursor_name INTO @name;
        CLOSE cursor_name;
        DEALLOCATE cursor_name;")]
    [InlineData("DECLARE myCursor CURSOR FAST_FORWARD FOR SELECT id FROM products")]
    public void Analyze_WhenCursorDeclared_ReturnsDiagnostic(string sql)
    {
        var rule = new DisallowCursorsRule();
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("disallow-cursors", diagnostics[0].Code);
        Assert.Contains("cursor", diagnostics[0].Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("SELECT * FROM users")]
    [InlineData("INSERT INTO logs (message) VALUES ('test')")]
    [InlineData("-- DECLARE cursor_name CURSOR FOR SELECT * FROM users")]
    [InlineData(@"
        DECLARE @table TABLE (id INT);
        INSERT INTO @table SELECT id FROM users;")]
    public void Analyze_WhenNoCursor_ReturnsNoDiagnostic(string sql)
    {
        var rule = new DisallowCursorsRule();
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_WhenMultipleCursors_ReturnsMultipleDiagnostics()
    {
        var sql = @"
            DECLARE cursor1 CURSOR FOR SELECT id FROM table1;
            DECLARE cursor2 CURSOR FOR SELECT name FROM table2;
            DECLARE cursor3 CURSOR FOR SELECT value FROM table3;
        ";

        var rule = new DisallowCursorsRule();
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Equal(3, diagnostics.Length);
        Assert.All(diagnostics, d => Assert.Equal("disallow-cursors", d.Code));
    }

    [Fact]
    public void GetFixes_ReturnsNoFixes()
    {
        var rule = new DisallowCursorsRule();
        var context = RuleTestContext.CreateContext("DECLARE cursor_name CURSOR FOR SELECT * FROM users");
        var diagnostic = rule.Analyze(context).First();
        var fixes = rule.GetFixes(context, diagnostic).ToArray();

        Assert.Empty(fixes);
    }


}
