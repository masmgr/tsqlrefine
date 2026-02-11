using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Correctness;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Correctness;

public sealed class AvoidNullComparisonRuleTests
{
    [Theory]
    [InlineData("SELECT * FROM users WHERE name = NULL;")]
    [InlineData("SELECT * FROM users WHERE status <> NULL;")]
    [InlineData("SELECT * FROM users WHERE email != NULL;")]
    [InlineData("UPDATE users SET active = 0 WHERE last_login = NULL;")]
    [InlineData("DELETE FROM sessions WHERE user_id <> NULL;")]
    [InlineData("SELECT * FROM users WHERE NULL = name;")]  // NULL on left side
    [InlineData("SELECT * FROM users WHERE NULL <> status;")]  // NULL on left side
    [InlineData("select * from users where name = null;")]  // lowercase
    public void Analyze_WhenNullComparison_ReturnsDiagnostic(string sql)
    {
        var rule = new AvoidNullComparisonRule();
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("avoid-null-comparison", diagnostics[0].Data?.RuleId);
        Assert.Equal("Correctness", diagnostics[0].Data?.Category);
        Assert.True(diagnostics[0].Data?.Fixable);
        Assert.Contains("NULL", diagnostics[0].Message);
    }

    [Theory]
    [InlineData("SELECT * FROM users WHERE name IS NULL;")]
    [InlineData("SELECT * FROM users WHERE status IS NOT NULL;")]
    [InlineData("SELECT * FROM users WHERE email IS NOT NULL;")]
    [InlineData("SELECT * FROM users WHERE name = 'value';")]
    [InlineData("SELECT * FROM users WHERE id = 123;")]
    [InlineData("SELECT * FROM users WHERE created_at > GETDATE();")]
    [InlineData("SELECT * FROM users WHERE status <> 'active';")]
    [InlineData("SELECT * FROM users WHERE price != 0;")]
    [InlineData("SELECT * FROM users;")]  // No WHERE clause
    public void Analyze_WhenNotViolating_ReturnsEmpty(string sql)
    {
        var rule = new AvoidNullComparisonRule();
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_EqualNullComparison_ReportsAtComparisonOperator()
    {
        var rule = new AvoidNullComparisonRule();
        var sql = "SELECT * FROM users WHERE name = NULL;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        var diagnostic = diagnostics[0];

        // The comparison expression should start at "name"
        Assert.Equal(0, diagnostic.Range.Start.Line);
        Assert.True(diagnostic.Range.Start.Character >= 26); // After "WHERE "
    }

    [Fact]
    public void Analyze_NotEqualNullComparison_ReturnsDiagnostic()
    {
        var rule = new AvoidNullComparisonRule();
        var sql = "SELECT * FROM users WHERE status <> NULL;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("avoid-null-comparison", diagnostics[0].Data?.RuleId);
    }

    [Fact]
    public void Analyze_NotEqualExclamationNullComparison_ReturnsDiagnostic()
    {
        var rule = new AvoidNullComparisonRule();
        var sql = "SELECT * FROM users WHERE email != NULL;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("avoid-null-comparison", diagnostics[0].Data?.RuleId);
    }

    [Fact]
    public void Analyze_MultipleViolations_ReturnsMultipleDiagnostics()
    {
        var rule = new AvoidNullComparisonRule();
        var sql = @"SELECT * FROM users WHERE name = NULL AND status <> NULL;
UPDATE users SET active = 0 WHERE last_login = NULL;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Equal(3, diagnostics.Length);
        Assert.All(diagnostics, d => Assert.Equal("avoid-null-comparison", d.Data?.RuleId));
    }

    [Fact]
    public void Analyze_EmptyInput_ReturnsEmpty()
    {
        var rule = new AvoidNullComparisonRule();
        var context = RuleTestContext.CreateContext("");

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void GetFixes_WhenWrongRuleId_ReturnsEmpty()
    {
        var rule = new AvoidNullComparisonRule();
        var context = RuleTestContext.CreateContext("SELECT * FROM users WHERE name = NULL;");
        var diagnostic = new Diagnostic(
            Range: new TsqlRefine.PluginSdk.Range(new Position(0, 0), new Position(0, 6)),
            Message: "test",
            Code: "wrong-rule-id"
        );

        var fixes = rule.GetFixes(context, diagnostic).ToArray();

        Assert.Empty(fixes);
    }

    [Fact]
    public void GetFixes_EqualNull_ReturnsIsNull()
    {
        var rule = new AvoidNullComparisonRule();
        var sql = "SELECT * FROM users WHERE name = NULL;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostic = rule.Analyze(context).Single();

        var fixes = rule.GetFixes(context, diagnostic).ToArray();

        var fix = Assert.Single(fixes);
        var edit = Assert.Single(fix.Edits);
        Assert.Equal("name IS NULL", edit.NewText);
    }

    [Fact]
    public void GetFixes_NotEqualBracketsNull_ReturnsIsNotNull()
    {
        var rule = new AvoidNullComparisonRule();
        var sql = "SELECT * FROM users WHERE status <> NULL;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostic = rule.Analyze(context).Single();

        var fixes = rule.GetFixes(context, diagnostic).ToArray();

        var fix = Assert.Single(fixes);
        var edit = Assert.Single(fix.Edits);
        Assert.Equal("status IS NOT NULL", edit.NewText);
    }

    [Fact]
    public void GetFixes_NotEqualExclamationNull_ReturnsIsNotNull()
    {
        var rule = new AvoidNullComparisonRule();
        var sql = "SELECT * FROM users WHERE email != NULL;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostic = rule.Analyze(context).Single();

        var fixes = rule.GetFixes(context, diagnostic).ToArray();

        var fix = Assert.Single(fixes);
        var edit = Assert.Single(fix.Edits);
        Assert.Equal("email IS NOT NULL", edit.NewText);
    }

    [Fact]
    public void GetFixes_NullOnLeftSide_ReturnsExprIsNull()
    {
        var rule = new AvoidNullComparisonRule();
        var sql = "SELECT * FROM users WHERE NULL = name;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostic = rule.Analyze(context).Single();

        var fixes = rule.GetFixes(context, diagnostic).ToArray();

        var fix = Assert.Single(fixes);
        var edit = Assert.Single(fix.Edits);
        Assert.Equal("name IS NULL", edit.NewText);
    }

    [Fact]
    public void GetFixes_NullOnLeftSideNotEqual_ReturnsExprIsNotNull()
    {
        var rule = new AvoidNullComparisonRule();
        var sql = "SELECT * FROM users WHERE NULL <> status;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostic = rule.Analyze(context).Single();

        var fixes = rule.GetFixes(context, diagnostic).ToArray();

        var fix = Assert.Single(fixes);
        var edit = Assert.Single(fix.Edits);
        Assert.Equal("status IS NOT NULL", edit.NewText);
    }

    [Fact]
    public void GetFixes_LowercaseNull_ReturnsIsNull()
    {
        var rule = new AvoidNullComparisonRule();
        var sql = "select * from users where name = null;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostic = rule.Analyze(context).Single();

        var fixes = rule.GetFixes(context, diagnostic).ToArray();

        var fix = Assert.Single(fixes);
        var edit = Assert.Single(fix.Edits);
        Assert.Equal("name IS NULL", edit.NewText);
    }

    [Fact]
    public void GetFixes_ComplexExpression_PreservesExpressionText()
    {
        var rule = new AvoidNullComparisonRule();
        var sql = "SELECT * FROM users WHERE COALESCE(a.name, b.name) = NULL;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostic = rule.Analyze(context).Single();

        var fixes = rule.GetFixes(context, diagnostic).ToArray();

        var fix = Assert.Single(fixes);
        var edit = Assert.Single(fix.Edits);
        Assert.Equal("COALESCE(a.name, b.name) IS NULL", edit.NewText);
    }

    [Fact]
    public void GetFixes_FixRangeMatchesDiagnosticRange()
    {
        var rule = new AvoidNullComparisonRule();
        var sql = "SELECT * FROM users WHERE name = NULL;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostic = rule.Analyze(context).Single();

        var fixes = rule.GetFixes(context, diagnostic).ToArray();

        var fix = Assert.Single(fixes);
        var edit = Assert.Single(fix.Edits);
        Assert.Equal(diagnostic.Range, edit.Range);
    }

    [Fact]
    public void Metadata_HasCorrectValues()
    {
        var rule = new AvoidNullComparisonRule();

        Assert.Equal("avoid-null-comparison", rule.Metadata.RuleId);
        Assert.Equal("Correctness", rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Error, rule.Metadata.DefaultSeverity);
        Assert.True(rule.Metadata.Fixable);
        Assert.Contains("NULL", rule.Metadata.Description);
    }
}
