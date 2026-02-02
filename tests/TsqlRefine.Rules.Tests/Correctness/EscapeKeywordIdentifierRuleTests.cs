using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Correctness;
using TsqlRefine.Rules.Tests.Helpers;
using Xunit;

namespace TsqlRefine.Rules.Tests.Correctness;

public sealed class EscapeKeywordIdentifierRuleTests
{
    private readonly EscapeKeywordIdentifierRule _rule = new();



    [Fact]
    public void Analyze_TableNameKeywordAfterFrom_ReturnsDiagnostic()
    {
        var sql = "SELECT * FROM order;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToList();

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("escape-keyword-identifier", diagnostic.Code);
        Assert.Contains("[order]", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Analyze_QualifiedColumnKeywordAfterDot_ReturnsDiagnostic()
    {
        var sql = "SELECT t.order FROM t;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToList();

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("escape-keyword-identifier", diagnostic.Code);
        Assert.Contains("[order]", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Analyze_CreateTableColumnNameKeyword_ReturnsDiagnostic()
    {
        var sql = "CREATE TABLE t (order int);";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToList();

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("escape-keyword-identifier", diagnostic.Code);
        Assert.Contains("[order]", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Analyze_AlreadyEscapedIdentifier_NoDiagnostic()
    {
        var sql = "SELECT * FROM [order];";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToList();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_TableValuedFunction_NoDiagnostic()
    {
        var sql = "SELECT * FROM OPENJSON(@j);";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToList();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_CreateTablePrimaryKeyConstraint_NoDiagnostic()
    {
        var sql = "CREATE TABLE t (id int, PRIMARY KEY (id));";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToList();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void GetFixes_ReturnsBracketEscapeEdit()
    {
        var sql = "SELECT * FROM order;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostic = _rule.Analyze(context).Single();

        var fixes = _rule.GetFixes(context, diagnostic).ToList();

        var fix = Assert.Single(fixes);
        Assert.Equal("Escape keyword identifier", fix.Title);
        var edit = Assert.Single(fix.Edits);
        Assert.Equal(diagnostic.Range, edit.Range);
        Assert.Equal("[order]", edit.NewText);
    }

    [Fact]
    public void Metadata_HasCorrectProperties()
    {
        Assert.Equal("escape-keyword-identifier", _rule.Metadata.RuleId);
        Assert.Equal("Correctness", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Warning, _rule.Metadata.DefaultSeverity);
        Assert.True(_rule.Metadata.Fixable);
    }

    [Fact]
    public void Analyze_InsertIntoStatement_DoesNotFlagInto()
    {
        var sql = "INSERT INTO TABLE1 VALUES (1);";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToList();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_SelectIntoStatement_DoesNotFlagInto()
    {
        var sql = "SELECT * INTO #temp FROM source;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToList();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_InsertIntoWithKeywordTableName_FlagsOnlyTableName()
    {
        var sql = "INSERT INTO order VALUES (1);";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToList();

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("escape-keyword-identifier", diagnostic.Code);
        Assert.Contains("[order]", diagnostic.Message, StringComparison.Ordinal);
    }
}
