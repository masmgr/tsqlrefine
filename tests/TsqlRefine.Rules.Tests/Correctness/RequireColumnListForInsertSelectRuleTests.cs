using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Correctness;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Correctness;

public sealed class RequireColumnListForInsertSelectRuleTests
{
    [Theory]
    [InlineData("INSERT INTO users SELECT * FROM temp;")]
    [InlineData("INSERT INTO users SELECT id, name FROM temp;")]
    [InlineData("INSERT INTO dbo.users SELECT * FROM temp;")]
    [InlineData("INSERT INTO [dbo].[users] SELECT id, name FROM temp;")]
    [InlineData("insert into users select * from temp;")]  // lowercase
    [InlineData("INSERT users SELECT * FROM temp;")]  // without INTO
    public void Analyze_WhenInsertSelectWithoutColumnList_ReturnsDiagnostic(string sql)
    {
        var rule = new RequireColumnListForInsertSelectRule();
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("require-column-list-for-insert-select", diagnostics[0].Data?.RuleId);
        Assert.Equal("Correctness", diagnostics[0].Data?.Category);
        Assert.False(diagnostics[0].Data?.Fixable);
        Assert.Contains("column list", diagnostics[0].Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("INSERT INTO users (id, name) SELECT id, name FROM temp;")]
    [InlineData("INSERT INTO users (id, name, email) SELECT id, name, email FROM temp;")]
    [InlineData("INSERT INTO dbo.users (id, name) SELECT * FROM temp;")]
    [InlineData("INSERT INTO [dbo].[users] (id, name) SELECT id, name FROM temp;")]
    [InlineData("INSERT users (id, name) SELECT id, name FROM temp;")]  // without INTO
    [InlineData("INSERT INTO users (id) SELECT id FROM temp;")]  // single column
    public void Analyze_WhenInsertSelectWithColumnList_ReturnsEmpty(string sql)
    {
        var rule = new RequireColumnListForInsertSelectRule();
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Theory]
    [InlineData("INSERT INTO users VALUES (1, 'John');")]
    [InlineData("INSERT INTO users (id, name) VALUES (1, 'John');")]
    [InlineData("SELECT * FROM users;")]  // not INSERT
    [InlineData("UPDATE users SET name = 'John';")]  // not INSERT
    public void Analyze_WhenNotInsertSelect_ReturnsEmpty(string sql)
    {
        var rule = new RequireColumnListForInsertSelectRule();
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_InsertSelectWithoutColumnList_ReportsAtInsertKeyword()
    {
        var rule = new RequireColumnListForInsertSelectRule();
        var sql = "INSERT INTO users SELECT * FROM temp;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        var diagnostic = diagnostics[0];

        // INSERT keyword starts at position 0,0 and ends at 0,6 ("INSERT" is 6 characters)
        Assert.Equal(0, diagnostic.Range.Start.Line);
        Assert.Equal(0, diagnostic.Range.Start.Character);
        Assert.Equal(0, diagnostic.Range.End.Line);
        Assert.Equal(6, diagnostic.Range.End.Character);
    }

    [Fact]
    public void Analyze_MultipleViolations_ReturnsMultipleDiagnostics()
    {
        var rule = new RequireColumnListForInsertSelectRule();
        var sql = @"INSERT INTO users SELECT * FROM temp;
INSERT INTO orders SELECT * FROM temp_orders;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Equal(2, diagnostics.Length);
        Assert.All(diagnostics, d => Assert.Equal("require-column-list-for-insert-select", d.Data?.RuleId));
    }

    [Fact]
    public void Analyze_MixedViolatingAndValid_ReturnsOnlyViolations()
    {
        var rule = new RequireColumnListForInsertSelectRule();
        var sql = @"INSERT INTO users SELECT * FROM temp;
INSERT INTO users (id, name) SELECT id, name FROM temp;
INSERT INTO orders SELECT * FROM temp_orders;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Equal(2, diagnostics.Length);
        Assert.All(diagnostics, d => Assert.Equal("require-column-list-for-insert-select", d.Data?.RuleId));
    }

    [Fact]
    public void Analyze_InsertSelectWithJoin_ReturnsDiagnostic()
    {
        var rule = new RequireColumnListForInsertSelectRule();
        var sql = @"INSERT INTO users
SELECT t1.id, t1.name
FROM temp t1
INNER JOIN temp2 t2 ON t1.id = t2.id;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("require-column-list-for-insert-select", diagnostics[0].Data?.RuleId);
    }

    [Fact]
    public void Analyze_InsertSelectWithSubquery_ReturnsDiagnostic()
    {
        var rule = new RequireColumnListForInsertSelectRule();
        var sql = @"INSERT INTO users
SELECT * FROM (
    SELECT id, name FROM temp WHERE active = 1
) AS subquery;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("require-column-list-for-insert-select", diagnostics[0].Data?.RuleId);
    }

    [Fact]
    public void Analyze_InsertSelectInStoredProcedure_ReturnsDiagnostic()
    {
        var rule = new RequireColumnListForInsertSelectRule();
        var sql = @"CREATE PROCEDURE CopyUsers
AS
BEGIN
    INSERT INTO users SELECT * FROM temp;
END;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("require-column-list-for-insert-select", diagnostics[0].Data?.RuleId);
    }

    [Fact]
    public void Analyze_InsertSelectWithCTE_ReturnsDiagnostic()
    {
        var rule = new RequireColumnListForInsertSelectRule();
        var sql = @"WITH temp_users AS (
    SELECT id, name FROM users WHERE active = 1
)
INSERT INTO archive_users SELECT * FROM temp_users;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("require-column-list-for-insert-select", diagnostics[0].Data?.RuleId);
    }

    [Fact]
    public void Analyze_InsertSelectWithTopClause_ReturnsDiagnostic()
    {
        var rule = new RequireColumnListForInsertSelectRule();
        var sql = "INSERT INTO users SELECT TOP 10 * FROM temp;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("require-column-list-for-insert-select", diagnostics[0].Data?.RuleId);
    }

    [Fact]
    public void Analyze_InsertSelectWithWhereClause_ReturnsDiagnostic()
    {
        var rule = new RequireColumnListForInsertSelectRule();
        var sql = "INSERT INTO users SELECT * FROM temp WHERE active = 1;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("require-column-list-for-insert-select", diagnostics[0].Data?.RuleId);
    }

    [Fact]
    public void Analyze_EmptyInput_ReturnsEmpty()
    {
        var rule = new RequireColumnListForInsertSelectRule();
        var context = RuleTestContext.CreateContext("");

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void GetFixes_ReturnsEmpty()
    {
        var rule = new RequireColumnListForInsertSelectRule();
        var context = RuleTestContext.CreateContext("INSERT INTO users SELECT * FROM temp;");
        var diagnostic = new Diagnostic(
            Range: new TsqlRefine.PluginSdk.Range(new Position(0, 0), new Position(0, 6)),
            Message: "test",
            Code: "require-column-list-for-insert-select"
        );

        var fixes = rule.GetFixes(context, diagnostic).ToArray();

        Assert.Empty(fixes);
    }

    [Fact]
    public void Metadata_HasCorrectValues()
    {
        var rule = new RequireColumnListForInsertSelectRule();

        Assert.Equal("require-column-list-for-insert-select", rule.Metadata.RuleId);
        Assert.Equal("Correctness", rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Warning, rule.Metadata.DefaultSeverity);
        Assert.False(rule.Metadata.Fixable);
        Assert.Contains("INSERT", rule.Metadata.Description);
        Assert.Contains("SELECT", rule.Metadata.Description);
    }


}
