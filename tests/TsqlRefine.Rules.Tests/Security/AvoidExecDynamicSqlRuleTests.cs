using System.IO;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules;
using TsqlRefine.Rules.Rules.Security;

namespace TsqlRefine.Rules.Tests.Security;

public sealed class AvoidExecDynamicSqlRuleTests
{
    private readonly AvoidExecDynamicSqlRule _rule = new();

    [Fact]
    public void Analyze_ExecWithVariable_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = "EXEC(@sql);";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
        var diagnostic = diagnostics[0];
        Assert.Equal("avoid-exec-dynamic-sql", diagnostic.Code);
        Assert.Contains("dynamic SQL", diagnostic.Message);
    }

    [Fact]
    public void Analyze_ExecuteWithVariable_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = "EXECUTE(@dynamicQuery);";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
        Assert.Equal("avoid-exec-dynamic-sql", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_ExecWithStringLiteral_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = "EXEC('SELECT * FROM users');";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
        Assert.Equal("avoid-exec-dynamic-sql", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_ExecWithConcatenatedVariables_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = "EXEC(@part1 + @part2);";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
        Assert.Equal("avoid-exec-dynamic-sql", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_ExecStoredProcedure_NoDiagnostic()
    {
        // Arrange
        const string sql = "EXEC dbo.GetUsers;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_ExecStoredProcedureWithParameters_NoDiagnostic()
    {
        // Arrange
        const string sql = "EXEC MyStoredProc @id = 1, @name = 'test';";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_ExecuteSpExecutesql_NoDiagnostic()
    {
        // Arrange
        const string sql = "EXECUTE sp_executesql @stmt;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_ExecWithParenthesesButProcedureName_NoDiagnostic()
    {
        // Arrange
        const string sql = "EXEC dbo.GetUsers();";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_MultipleExecStatements_ReturnsMultipleDiagnostics()
    {
        // Arrange
        const string sql = @"
            EXEC(@sql1);
            EXEC dbo.ValidProc;
            EXEC('SELECT 1');
        ";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Equal(2, diagnostics.Length);
        Assert.All(diagnostics, d => Assert.Equal("avoid-exec-dynamic-sql", d.Code));
    }

    [Fact]
    public void Analyze_EmptyInput_ReturnsEmpty()
    {
        // Arrange
        var context = CreateContext("");

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void GetFixes_ReturnsEmptyCollection()
    {
        // Arrange
        const string sql = "EXEC(@sql);";
        var context = CreateContext(sql);
        var diagnostic = _rule.Analyze(context).First();

        // Act
        var fixes = _rule.GetFixes(context, diagnostic);

        // Assert
        Assert.Empty(fixes);
    }

    [Fact]
    public void Metadata_HasCorrectProperties()
    {
        // Assert
        Assert.Equal("avoid-exec-dynamic-sql", _rule.Metadata.RuleId);
        Assert.Equal("Security", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Warning, _rule.Metadata.DefaultSeverity);
        Assert.False(_rule.Metadata.Fixable);
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
