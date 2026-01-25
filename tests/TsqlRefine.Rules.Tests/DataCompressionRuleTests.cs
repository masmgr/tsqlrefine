using System.IO;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules;

namespace TsqlRefine.Rules.Tests;

public sealed class DataCompressionRuleTests
{
    [Fact]
    public void Metadata_ShouldHaveCorrectProperties()
    {
        var rule = new DataCompressionRule();

        Assert.Equal("data-compression", rule.Metadata.RuleId);
        Assert.Equal("Performance", rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Information, rule.Metadata.DefaultSeverity);
        Assert.False(rule.Metadata.Fixable);
    }

    [Theory]
    [InlineData("CREATE TABLE dbo.Users (Id INT, Name VARCHAR(100))")]
    [InlineData(@"
        CREATE TABLE dbo.Orders (
            OrderId INT PRIMARY KEY,
            CustomerId INT,
            OrderDate DATETIME
        )")]
    [InlineData(@"
        CREATE TABLE dbo.Products (
            Id INT IDENTITY(1,1),
            Name NVARCHAR(200)
        )")]
    public void Analyze_WhenTableWithoutDataCompression_ReturnsDiagnostic(string sql)
    {
        var rule = new DataCompressionRule();
        var context = CreateContext(sql);
        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("data-compression", diagnostics[0].Code);
        Assert.Contains("DATA_COMPRESSION", diagnostics[0].Message);
    }

    [Theory]
    [InlineData(@"
        CREATE TABLE dbo.Users (
            Id INT,
            Name VARCHAR(100)
        ) WITH (DATA_COMPRESSION = ROW)")]
    [InlineData(@"
        CREATE TABLE dbo.Orders (
            OrderId INT PRIMARY KEY,
            CustomerId INT
        ) WITH (DATA_COMPRESSION = PAGE)")]
    [InlineData(@"
        CREATE TABLE dbo.Products (
            Id INT
        ) WITH (DATA_COMPRESSION = NONE)")]
    public void Analyze_WhenTableWithDataCompression_ReturnsNoDiagnostic(string sql)
    {
        var rule = new DataCompressionRule();
        var context = CreateContext(sql);
        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Theory]
    [InlineData("SELECT * FROM users")]
    [InlineData("INSERT INTO logs (message) VALUES ('test')")]
    [InlineData("CREATE PROCEDURE dbo.GetUsers AS SELECT * FROM users")]
    public void Analyze_WhenNoTableCreation_ReturnsNoDiagnostic(string sql)
    {
        var rule = new DataCompressionRule();
        var context = CreateContext(sql);
        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_WhenMultipleTablesWithoutCompression_ReturnsMultipleDiagnostics()
    {
        var sql = @"
            CREATE TABLE dbo.Table1 (Id INT);
            CREATE TABLE dbo.Table2 (Name VARCHAR(50));
        ";

        var rule = new DataCompressionRule();
        var context = CreateContext(sql);
        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Equal(2, diagnostics.Length);
        Assert.All(diagnostics, d => Assert.Equal("data-compression", d.Code));
    }

    [Fact]
    public void GetFixes_ReturnsNoFixes()
    {
        var rule = new DataCompressionRule();
        var context = CreateContext("CREATE TABLE dbo.Users (Id INT)");
        var diagnostic = rule.Analyze(context).First();
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
