using System.IO;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules;

namespace TsqlRefine.Rules.Tests;

public sealed class NamedConstraintRuleTests
{
    [Fact]
    public void Metadata_ShouldHaveCorrectProperties()
    {
        var rule = new NamedConstraintRule();

        Assert.Equal("named-constraint", rule.Metadata.RuleId);
        Assert.Equal("Correctness", rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Error, rule.Metadata.DefaultSeverity);
        Assert.False(rule.Metadata.Fixable);
    }

    [Theory]
    [InlineData(@"
        CREATE TABLE #TempTable (
            Id INT CONSTRAINT PK_TempTable PRIMARY KEY
        )")]
    [InlineData(@"
        CREATE TABLE #MyTemp (
            Id INT,
            Name VARCHAR(50),
            CONSTRAINT UK_MyTemp_Name UNIQUE (Name)
        )")]
    [InlineData(@"
        CREATE TABLE ##GlobalTemp (
            Id INT,
            CONSTRAINT CK_GlobalTemp_Id CHECK (Id > 0)
        )")]
    public void Analyze_WhenTempTableWithNamedConstraint_ReturnsDiagnostic(string sql)
    {
        var rule = new NamedConstraintRule();
        var context = CreateContext(sql);
        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("named-constraint", diagnostics[0].Code);
        Assert.Contains("temp table", diagnostics[0].Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(@"
        CREATE TABLE dbo.Users (
            Id INT CONSTRAINT PK_Users PRIMARY KEY
        )")]
    [InlineData(@"
        CREATE TABLE dbo.Orders (
            Id INT,
            CONSTRAINT UK_Orders_Id UNIQUE (Id)
        )")]
    [InlineData(@"
        CREATE TABLE #TempTable (
            Id INT PRIMARY KEY
        )")]
    [InlineData(@"
        CREATE TABLE #TempTable (
            Id INT,
            Name VARCHAR(50) UNIQUE
        )")]
    public void Analyze_WhenNoIssue_ReturnsNoDiagnostic(string sql)
    {
        var rule = new NamedConstraintRule();
        var context = CreateContext(sql);
        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_WhenMultipleNamedConstraintsInTempTable_ReturnsMultipleDiagnostics()
    {
        var sql = @"
            CREATE TABLE #TempTable (
                Id INT CONSTRAINT PK_Temp PRIMARY KEY,
                Name VARCHAR(50) CONSTRAINT UK_Temp_Name UNIQUE
            )
        ";

        var rule = new NamedConstraintRule();
        var context = CreateContext(sql);
        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Equal(2, diagnostics.Length);
        Assert.All(diagnostics, d => Assert.Equal("named-constraint", d.Code));
    }

    [Fact]
    public void GetFixes_ReturnsNoFixes()
    {
        var rule = new NamedConstraintRule();
        var context = CreateContext("CREATE TABLE #Temp (Id INT CONSTRAINT PK_Temp PRIMARY KEY)");
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
                    text.Length);
            })
            .ToArray();
    }
}
