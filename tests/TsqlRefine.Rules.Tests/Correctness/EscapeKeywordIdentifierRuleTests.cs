using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Correctness;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Correctness;

public sealed class EscapeKeywordIdentifierRuleTests
{
    private readonly EscapeKeywordIdentifierRule _rule = new();



    [Fact]
    public void Analyze_TableNameKeywordAfterFrom_ReturnsDiagnostic()
    {
        var sql = "SELECT * FROM value;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToList();

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("escape-keyword-identifier", diagnostic.Code);
        Assert.Contains("[value]", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Analyze_QualifiedColumnKeywordAfterDot_ReturnsDiagnostic()
    {
        var sql = "SELECT t.value FROM t;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToList();

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("escape-keyword-identifier", diagnostic.Code);
        Assert.Contains("[value]", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Analyze_CreateTableColumnNameKeyword_ReturnsDiagnostic()
    {
        var sql = "CREATE TABLE t (value int);";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToList();

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("escape-keyword-identifier", diagnostic.Code);
        Assert.Contains("[value]", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Analyze_AlreadyEscapedIdentifier_NoDiagnostic()
    {
        var sql = "SELECT * FROM [value];";
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
        var sql = "SELECT * FROM value;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostic = _rule.Analyze(context).Single();

        var fixes = _rule.GetFixes(context, diagnostic).ToList();

        var fix = Assert.Single(fixes);
        Assert.Equal("Escape keyword identifier", fix.Title);
        var edit = Assert.Single(fix.Edits);
        Assert.Equal(diagnostic.Range, edit.Range);
        Assert.Equal("[value]", edit.NewText);
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
        var sql = "INSERT INTO value VALUES (1);";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToList();

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("escape-keyword-identifier", diagnostic.Code);
        Assert.Contains("[value]", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Analyze_CreateFunctionWithReturnsTable_NoDiagnostic()
    {
        var sql = @"CREATE FUNCTION dbo.GetItems()
RETURNS TABLE
AS
RETURN (SELECT id, title FROM items);";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToList();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_CreateFunctionWithKeywordColumn_FlagsOnlyColumn()
    {
        var sql = @"CREATE FUNCTION dbo.GetOrders()
RETURNS TABLE
AS
RETURN (SELECT id, t.value FROM items AS t);";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToList();

        // Should only flag t.value (qualified column reference)
        var diagnostic = Assert.Single(diagnostics);
        Assert.Contains("[value]", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Analyze_CreateProcedureWithSelect_NoDiagnostic()
    {
        var sql = @"CREATE PROCEDURE dbo.GetData
AS
BEGIN
    SELECT id FROM items;
END;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToList();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_MultiStatementTableValuedFunction_NoDiagnostic()
    {
        var sql = @"CREATE FUNCTION dbo.GetItems()
RETURNS @results TABLE (id INT, title NVARCHAR(100))
AS
BEGIN
    INSERT INTO @results SELECT id, title FROM items;
    RETURN;
END;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToList();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_NullInExpression_NoDiagnostic()
    {
        var sql = "SELECT ISNULL(col, NULL) FROM t;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToList();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_CaseExpression_NoDiagnostic()
    {
        var sql = "SELECT CASE WHEN x = 1 THEN 'a' ELSE 'b' END FROM t;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToList();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_CreateTableWithEscapedAndUnescapedKeyword_FlagsOnlyUnescaped()
    {
        var sql = "CREATE TABLE dbo.test ([type] INT, value INT);";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToList();

        // Should flag only 'value' (unescaped), not '[type]' (escaped)
        var diagnostic = Assert.Single(diagnostics);
        Assert.Contains("[value]", diagnostic.Message, StringComparison.Ordinal);
    }
}
