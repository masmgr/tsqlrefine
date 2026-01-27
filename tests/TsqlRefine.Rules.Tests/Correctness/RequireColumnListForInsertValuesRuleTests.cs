using System.IO;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules;
using TsqlRefine.Rules.Rules.Correctness;

namespace TsqlRefine.Rules.Tests.Correctness;

public sealed class RequireColumnListForInsertValuesRuleTests
{
    [Theory]
    [InlineData("INSERT INTO users VALUES (1, 'John');")]
    [InlineData("INSERT INTO users VALUES (1, 'John', 'john@example.com');")]
    [InlineData("INSERT INTO dbo.users VALUES (1, 'John');")]
    [InlineData("INSERT INTO [dbo].[users] VALUES (1, 'John');")]
    [InlineData("insert into users values (1, 'John');")]  // lowercase
    [InlineData("INSERT users VALUES (1, 'John');")]  // without INTO
    public void Analyze_WhenInsertValuesWithoutColumnList_ReturnsDiagnostic(string sql)
    {
        var rule = new RequireColumnListForInsertValuesRule();
        var context = CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("require-column-list-for-insert-values", diagnostics[0].Data?.RuleId);
        Assert.Equal("Correctness", diagnostics[0].Data?.Category);
        Assert.False(diagnostics[0].Data?.Fixable);
        Assert.Contains("column list", diagnostics[0].Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("INSERT INTO users (id, name) VALUES (1, 'John');")]
    [InlineData("INSERT INTO users (id, name, email) VALUES (1, 'John', 'john@example.com');")]
    [InlineData("INSERT INTO dbo.users (id, name) VALUES (1, 'John');")]
    [InlineData("INSERT INTO [dbo].[users] (id, name) VALUES (1, 'John');")]
    [InlineData("INSERT users (id, name) VALUES (1, 'John');")]  // without INTO
    [InlineData("INSERT INTO users (id) VALUES (1);")]  // single column
    public void Analyze_WhenInsertValuesWithColumnList_ReturnsEmpty(string sql)
    {
        var rule = new RequireColumnListForInsertValuesRule();
        var context = CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Theory]
    [InlineData("INSERT INTO users SELECT * FROM temp;")]
    [InlineData("INSERT INTO users (id, name) SELECT id, name FROM temp;")]
    [InlineData("SELECT * FROM users;")]  // not INSERT
    [InlineData("UPDATE users SET name = 'John';")]  // not INSERT
    public void Analyze_WhenNotInsertValues_ReturnsEmpty(string sql)
    {
        var rule = new RequireColumnListForInsertValuesRule();
        var context = CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_InsertValuesWithoutColumnList_ReportsAtInsertKeyword()
    {
        var rule = new RequireColumnListForInsertValuesRule();
        var sql = "INSERT INTO users VALUES (1, 'John');";
        var context = CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        var diagnostic = diagnostics[0];

        // INSERT keyword starts at position 0,0
        Assert.Equal(0, diagnostic.Range.Start.Line);
        Assert.Equal(0, diagnostic.Range.Start.Character);
    }

    [Fact]
    public void Analyze_MultipleViolations_ReturnsMultipleDiagnostics()
    {
        var rule = new RequireColumnListForInsertValuesRule();
        var sql = @"INSERT INTO users VALUES (1, 'John');
INSERT INTO orders VALUES (100, 'Order1');";
        var context = CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Equal(2, diagnostics.Length);
        Assert.All(diagnostics, d => Assert.Equal("require-column-list-for-insert-values", d.Data?.RuleId));
    }

    [Fact]
    public void Analyze_MixedViolatingAndValid_ReturnsOnlyViolations()
    {
        var rule = new RequireColumnListForInsertValuesRule();
        var sql = @"INSERT INTO users VALUES (1, 'John');
INSERT INTO users (id, name) VALUES (2, 'Jane');
INSERT INTO orders VALUES (100, 'Order1');";
        var context = CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Equal(2, diagnostics.Length);
        Assert.All(diagnostics, d => Assert.Equal("require-column-list-for-insert-values", d.Data?.RuleId));
    }

    [Fact]
    public void Analyze_InsertValuesWithMultipleRows_ReturnsSingleDiagnostic()
    {
        var rule = new RequireColumnListForInsertValuesRule();
        var sql = @"INSERT INTO users VALUES
    (1, 'John'),
    (2, 'Jane'),
    (3, 'Bob');";
        var context = CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("require-column-list-for-insert-values", diagnostics[0].Data?.RuleId);
    }

    [Fact]
    public void Analyze_InsertValuesInStoredProcedure_ReturnsDiagnostic()
    {
        var rule = new RequireColumnListForInsertValuesRule();
        var sql = @"CREATE PROCEDURE InsertUser
AS
BEGIN
    INSERT INTO users VALUES (1, 'John');
END;";
        var context = CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("require-column-list-for-insert-values", diagnostics[0].Data?.RuleId);
    }

    [Fact]
    public void Analyze_InsertValuesWithCTE_ReturnsDiagnostic()
    {
        var rule = new RequireColumnListForInsertValuesRule();
        var sql = @"WITH temp AS (SELECT 1 AS id)
INSERT INTO users VALUES (1, 'John');";
        var context = CreateContext(sql);

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("require-column-list-for-insert-values", diagnostics[0].Data?.RuleId);
    }

    [Fact]
    public void Analyze_EmptyInput_ReturnsEmpty()
    {
        var rule = new RequireColumnListForInsertValuesRule();
        var context = CreateContext("");

        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void GetFixes_ReturnsEmpty()
    {
        var rule = new RequireColumnListForInsertValuesRule();
        var context = CreateContext("INSERT INTO users VALUES (1, 'John');");
        var diagnostic = new Diagnostic(
            Range: new TsqlRefine.PluginSdk.Range(new Position(0, 0), new Position(0, 6)),
            Message: "test",
            Code: "require-column-list-for-insert-values"
        );

        var fixes = rule.GetFixes(context, diagnostic).ToArray();

        Assert.Empty(fixes);
    }

    [Fact]
    public void Metadata_HasCorrectValues()
    {
        var rule = new RequireColumnListForInsertValuesRule();

        Assert.Equal("require-column-list-for-insert-values", rule.Metadata.RuleId);
        Assert.Equal("Correctness", rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Warning, rule.Metadata.DefaultSeverity);
        Assert.False(rule.Metadata.Fixable);
        Assert.Contains("INSERT", rule.Metadata.Description);
        Assert.Contains("VALUES", rule.Metadata.Description);
    }

    private static RuleContext CreateContext(string sql)
    {
        var parser = new TSql150Parser(initialQuotedIdentifiers: true);
        using var reader = new StringReader(sql);
        var fragment = parser.Parse(reader, out var parseErrors);

        var ast = new ScriptDomAst(sql, fragment, parseErrors as IReadOnlyList<ParseError>, Array.Empty<ParseError>());
        var tokens = Tokenize(sql);

        return new RuleContext(
            FilePath: "<test>",
            CompatLevel: 150,
            Ast: ast,
            Tokens: tokens,
            Settings: new RuleSettings()
        );
    }

    private static IReadOnlyList<Token> Tokenize(string sql)
    {
        var parser = new TSql150Parser(initialQuotedIdentifiers: true);
        using var reader = new StringReader(sql);
        var tokenStream = parser.GetTokenStream(reader, out _);
        return tokenStream
            .Where(token => token.TokenType != TSqlTokenType.EndOfFile)
            .Select(token =>
            {
                var text = token.Text ?? string.Empty;
                return new Token(
                    text,
                    new Position(Math.Max(0, token.Line - 1), Math.Max(0, token.Column - 1)),
                    text.Length,
                    token.TokenType.ToString());
            })
            .ToArray();
    }
}
