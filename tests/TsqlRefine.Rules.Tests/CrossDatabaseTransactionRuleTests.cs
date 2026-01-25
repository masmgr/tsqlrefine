using System.IO;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules;

namespace TsqlRefine.Rules.Tests;

public sealed class CrossDatabaseTransactionRuleTests
{
    [Fact]
    public void Metadata_ShouldHaveCorrectProperties()
    {
        var rule = new CrossDatabaseTransactionRule();

        Assert.Equal("cross-database-transaction", rule.Metadata.RuleId);
        Assert.Equal("Safety", rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Warning, rule.Metadata.DefaultSeverity);
        Assert.False(rule.Metadata.Fixable);
    }

    [Theory]
    [InlineData(@"
        BEGIN TRANSACTION
        INSERT INTO DB1.dbo.Table1 VALUES (1)
        INSERT INTO DB2.dbo.Table2 VALUES (2)
        COMMIT")]
    [InlineData(@"
        BEGIN TRAN
        UPDATE Database1.dbo.Users SET Name = 'Test'
        UPDATE Database2.dbo.Orders SET Status = 'Done'
        COMMIT")]
    [InlineData(@"
        BEGIN TRANSACTION
        DELETE FROM [DB1].[dbo].[Table1]
        DELETE FROM [DB2].[dbo].[Table2]
        ROLLBACK")]
    public void Analyze_WhenCrossDatabaseTransaction_ReturnsDiagnostic(string sql)
    {
        var rule = new CrossDatabaseTransactionRule();
        var context = CreateContext(sql);
        var diagnostics = rule.Analyze(context).ToArray();

        Assert.NotEmpty(diagnostics);
        Assert.All(diagnostics, d => Assert.Equal("cross-database-transaction", d.Code));
        Assert.All(diagnostics, d => Assert.Contains("cross-database", d.Message, StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(@"
        BEGIN TRANSACTION
        INSERT INTO dbo.Table1 VALUES (1)
        INSERT INTO dbo.Table2 VALUES (2)
        COMMIT")]
    [InlineData(@"
        BEGIN TRANSACTION
        UPDATE MyDB.dbo.Users SET Name = 'Test'
        UPDATE MyDB.dbo.Orders SET Status = 'Done'
        COMMIT")]
    [InlineData(@"
        INSERT INTO DB1.dbo.Table1 VALUES (1)
        INSERT INTO DB2.dbo.Table2 VALUES (2)")]
    [InlineData("SELECT * FROM DB1.dbo.Table1")]
    public void Analyze_WhenNoIssue_ReturnsNoDiagnostic(string sql)
    {
        var rule = new CrossDatabaseTransactionRule();
        var context = CreateContext(sql);
        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_WhenNestedTransaction_DetectsCrossDatabase()
    {
        var sql = @"
            BEGIN TRANSACTION
                INSERT INTO DB1.dbo.Table1 VALUES (1)
                BEGIN TRANSACTION
                    UPDATE DB2.dbo.Table2 SET Value = 1
                COMMIT
            COMMIT
        ";

        var rule = new CrossDatabaseTransactionRule();
        var context = CreateContext(sql);
        var diagnostics = rule.Analyze(context).ToArray();

        Assert.NotEmpty(diagnostics);
        Assert.All(diagnostics, d => Assert.Equal("cross-database-transaction", d.Code));
    }

    [Fact]
    public void GetFixes_ReturnsNoFixes()
    {
        var sql = @"
            BEGIN TRANSACTION
            INSERT INTO DB1.dbo.Table1 VALUES (1)
            INSERT INTO DB2.dbo.Table2 VALUES (2)
            COMMIT";

        var rule = new CrossDatabaseTransactionRule();
        var context = CreateContext(sql);
        var diagnostics = rule.Analyze(context);
        var diagnostic = diagnostics.First();
        var fixes = rule.GetFixes(context, diagnostic).ToArray();

        Assert.Empty(fixes);
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
