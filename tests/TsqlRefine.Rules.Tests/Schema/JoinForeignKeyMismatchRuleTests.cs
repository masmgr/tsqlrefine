using TsqlRefine.Rules.Rules.Schema;
using TsqlRefine.Rules.Tests.Helpers;
using TsqlRefine.Schema.Resolution;
using TsqlRefine.Schema.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Schema;

public sealed class JoinForeignKeyMismatchRuleTests
{
    private readonly JoinForeignKeyMismatchRule _rule = new();

    private static SchemaProvider CreateSchema() =>
        new(TestSchemaBuilder.Create()
            .AddTable("dbo", "TableA", t => t
                .AddColumn("Id", "int")
                .AddColumn("ID_B", "int")
                .AddColumn("Name", "nvarchar", maxLength: 100)
                .WithPrimaryKey(true, "Id")
                .AddForeignKey("FK_A_B", ["ID_B"], "dbo", "TableB", ["Id"]))
            .AddTable("dbo", "TableB", t => t
                .AddColumn("Id", "int")
                .AddColumn("Value", "nvarchar", maxLength: 200)
                .WithPrimaryKey(true, "Id"))
            .AddTable("dbo", "TableC", t => t
                .AddColumn("Id", "int")
                .AddColumn("Value", "nvarchar", maxLength: 200)
                .WithPrimaryKey(true, "Id"))
            .AddTable("dbo", "SelfRef", t => t
                .AddColumn("Id", "int")
                .AddColumn("ParentId", "int")
                .WithPrimaryKey(true, "Id")
                .AddForeignKey("FK_SelfRef_Parent", ["ParentId"], "dbo", "SelfRef", ["Id"]))
            .Build());

    // ===== Positive cases: should detect mismatch =====

    [Fact]
    public void Analyze_JoinToWrongTable_ReturnsDiagnostic()
    {
        const string sql = "SELECT a.Name FROM dbo.TableA AS a INNER JOIN dbo.TableC AS c ON c.Id = a.ID_B;";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.NotEmpty(diagnostics);
        Assert.Contains(diagnostics, d => d.Code == "join-foreign-key-mismatch");
    }

    [Fact]
    public void Analyze_JoinToWrongTableReversedOnClause_ReturnsDiagnostic()
    {
        const string sql = "SELECT a.Name FROM dbo.TableA AS a INNER JOIN dbo.TableC AS c ON a.ID_B = c.Id;";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.NotEmpty(diagnostics);
        Assert.Contains(diagnostics, d => d.Code == "join-foreign-key-mismatch");
    }

    [Fact]
    public void Analyze_JoinToWrongTableWithoutAlias_ReturnsDiagnostic()
    {
        const string sql = "SELECT TableA.Name FROM dbo.TableA INNER JOIN dbo.TableC ON TableC.Id = TableA.ID_B;";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.NotEmpty(diagnostics);
        Assert.Contains(diagnostics, d => d.Code == "join-foreign-key-mismatch");
    }

    [Fact]
    public void Analyze_LeftJoinToWrongTable_ReturnsDiagnostic()
    {
        const string sql = "SELECT a.Name FROM dbo.TableA AS a LEFT JOIN dbo.TableC AS c ON c.Id = a.ID_B;";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.NotEmpty(diagnostics);
    }

    [Fact]
    public void Analyze_MultipleJoins_ReportsOnlyMismatch()
    {
        const string sql = """
            SELECT a.Name, b.Value, c.Value
            FROM dbo.TableA AS a
            INNER JOIN dbo.TableB AS b ON b.Id = a.ID_B
            INNER JOIN dbo.TableC AS c ON c.Id = a.ID_B;
            """;
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        // Only the second JOIN (to TableC) should be flagged
        Assert.NotEmpty(diagnostics);
        Assert.All(diagnostics, d =>
        {
            Assert.Equal("join-foreign-key-mismatch", d.Code);
            Assert.Contains("TableC", d.Message);
        });
    }

    [Fact]
    public void Analyze_DiagnosticContainsForeignKeyName()
    {
        const string sql = "SELECT a.Name FROM dbo.TableA AS a INNER JOIN dbo.TableC AS c ON c.Id = a.ID_B;";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Contains(diagnostics, d => d.Message.Contains("FK_A_B"));
    }

    [Fact]
    public void Analyze_DiagnosticContainsExpectedTable()
    {
        const string sql = "SELECT a.Name FROM dbo.TableA AS a INNER JOIN dbo.TableC AS c ON c.Id = a.ID_B;";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Contains(diagnostics, d => d.Message.Contains("TableB"));
    }

    // ===== Negative cases: should NOT detect =====

    [Fact]
    public void Analyze_CorrectForeignKeyJoin_ReturnsEmpty()
    {
        const string sql = "SELECT a.Name FROM dbo.TableA AS a INNER JOIN dbo.TableB AS b ON b.Id = a.ID_B;";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_JoinWithNoForeignKeyColumns_ReturnsEmpty()
    {
        const string sql = "SELECT a.Name FROM dbo.TableA AS a INNER JOIN dbo.TableC AS c ON c.Value = a.Name;";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_SelfReferencingForeignKey_ReturnsEmpty()
    {
        const string sql = "SELECT s1.Id FROM dbo.SelfRef AS s1 INNER JOIN dbo.SelfRef AS s2 ON s2.Id = s1.ParentId;";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_NoSchema_ReturnsEmpty()
    {
        const string sql = "SELECT a.Name FROM dbo.TableA AS a INNER JOIN dbo.TableC AS c ON c.Id = a.ID_B;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_TempTable_ReturnsEmpty()
    {
        const string sql = "SELECT t.Id FROM #Temp AS t INNER JOIN dbo.TableB AS b ON b.Id = t.ID_B;";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_UnrelatedColumnNames_ReturnsEmpty()
    {
        // A.ID_B has FK to B.Id, but joined column is C.Value (not "Id"), so no match
        const string sql = "SELECT a.Name FROM dbo.TableA AS a INNER JOIN dbo.TableC AS c ON c.Value = a.ID_B;";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_NoFromClause_ReturnsEmpty()
    {
        const string sql = "SELECT 1;";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_DerivedTable_ReturnsEmpty()
    {
        const string sql = "SELECT a.Name FROM dbo.TableA AS a INNER JOIN (SELECT Id FROM dbo.TableC) AS sub ON sub.Id = a.ID_B;";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_UnresolvedTable_ReturnsEmpty()
    {
        const string sql = "SELECT a.Name FROM dbo.TableA AS a INNER JOIN dbo.NonExistent AS n ON n.Id = a.ID_B;";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_CrossJoin_ReturnsEmpty()
    {
        const string sql = "SELECT a.Name FROM dbo.TableA AS a CROSS JOIN dbo.TableC AS c;";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }
}
