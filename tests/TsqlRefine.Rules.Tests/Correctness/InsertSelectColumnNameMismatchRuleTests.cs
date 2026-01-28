using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Correctness;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Correctness;

public sealed class InsertSelectColumnNameMismatchRuleTests
{
    [Theory]
    [InlineData("INSERT INTO Users (Id, Name) SELECT Name, Id FROM Users_Staging;")]
    [InlineData("INSERT INTO Orders (OrderDate, ShipDate) SELECT ShipDate, OrderDate FROM ImportedOrders;")]
    public void Analyze_WhenColumnNamesMismatch_ReturnsDiagnostic(string sql)
    {
        var rule = new InsertSelectColumnNameMismatchRule();
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.NotEmpty(diagnostics);
        Assert.Contains(diagnostics, d => d.Data?.RuleId == "insert-select-column-name-mismatch");
        Assert.All(diagnostics.Where(d => d.Data?.RuleId == "insert-select-column-name-mismatch"), d =>
        {
            Assert.Equal("Correctness", d.Data?.Category);
            Assert.False(d.Data?.Fixable);
            Assert.Contains("do not match", d.Message, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Theory]
    [InlineData("INSERT INTO Users (Id, Name) SELECT Id, Name FROM Users_Staging;")]
    [InlineData("INSERT INTO Users (Id, Name) SELECT u.Id, u.Name FROM Users_Staging u;")]
    [InlineData("INSERT INTO Users (Id, Name) SELECT u.Id AS Id, u.Name AS Name FROM Users_Staging u;")]
    [InlineData("INSERT INTO Users (ID, NAME) SELECT Id, Name FROM Users_Staging;")]
    public void Analyze_WhenColumnNamesMatch_ReturnsEmpty(string sql)
    {
        var rule = new InsertSelectColumnNameMismatchRule();
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).Where(d => d.Data?.RuleId == "insert-select-column-name-mismatch").ToArray();

        Assert.Empty(diagnostics);
    }

    [Theory]
    [InlineData("INSERT INTO Logs (CreatedAt, Message) SELECT GETDATE(), Message FROM TempLogs;")]
    [InlineData("INSERT INTO Logs (CreatedAt, Message) SELECT 'now', Message FROM TempLogs;")]
    [InlineData("INSERT INTO Logs (CreatedAt, Message) SELECT CreatedAt + 1, Message FROM TempLogs;")]
    [InlineData("INSERT INTO Logs (CreatedAt, Message) SELECT * FROM TempLogs;")]
    public void Analyze_WhenSelectHasComplexExpressions_IsSkipped(string sql)
    {
        var rule = new InsertSelectColumnNameMismatchRule();
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = rule.Analyze(context).Where(d => d.Data?.RuleId == "insert-select-column-name-mismatch").ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_WhenColumnCountMismatch_IsSkipped()
    {
        var rule = new InsertSelectColumnNameMismatchRule();
        var context = RuleTestContext.CreateContext("INSERT INTO Users (Id, Name) SELECT Id FROM Users_Staging;");

        var diagnostics = rule.Analyze(context).Where(d => d.Data?.RuleId == "insert-select-column-name-mismatch").ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_EmptyInput_ReturnsEmpty()
    {
        var rule = new InsertSelectColumnNameMismatchRule();
        var context = RuleTestContext.CreateContext(string.Empty);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void GetFixes_ReturnsEmpty()
    {
        var rule = new InsertSelectColumnNameMismatchRule();
        var context = RuleTestContext.CreateContext("INSERT INTO Users (Id, Name) SELECT Name, Id FROM Users_Staging;");
        var diagnostic = new Diagnostic(
            Range: new TsqlRefine.PluginSdk.Range(new Position(0, 0), new Position(0, 10)),
            Message: "test",
            Code: "insert-select-column-name-mismatch"
        );

        var fixes = rule.GetFixes(context, diagnostic).ToArray();

        Assert.Empty(fixes);
    }

    [Fact]
    public void Metadata_HasCorrectValues()
    {
        var rule = new InsertSelectColumnNameMismatchRule();

        Assert.Equal("insert-select-column-name-mismatch", rule.Metadata.RuleId);
        Assert.Equal("Correctness", rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Information, rule.Metadata.DefaultSeverity);
        Assert.False(rule.Metadata.Fixable);
        Assert.Contains("INSERT", rule.Metadata.Description, StringComparison.OrdinalIgnoreCase);
    }
}
