using System.IO;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules;

namespace TsqlRefine.Rules.Tests;

public sealed class AvoidMergeRuleTests
{
    private readonly AvoidMergeRule _rule = new();

    [Fact]
    public void Analyze_SimpleMerge_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = @"
MERGE INTO target t
USING source s
ON t.id = s.id
WHEN MATCHED THEN UPDATE SET t.value = s.value;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
        Assert.Equal("avoid-merge", diagnostics[0].Code);
        Assert.Contains("MERGE", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_MergeWithInsert_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = @"
MERGE INTO users t
USING new_users s ON t.id = s.id
WHEN MATCHED THEN UPDATE SET t.name = s.name
WHEN NOT MATCHED THEN INSERT (id, name) VALUES (s.id, s.name);";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
        Assert.Equal("avoid-merge", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_MergeWithDelete_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = @"
MERGE INTO users t
USING deleted_users s ON t.id = s.id
WHEN MATCHED THEN DELETE;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
        Assert.Equal("avoid-merge", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_MergeWithAllActions_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = @"
MERGE INTO target t
USING source s ON t.id = s.id
WHEN MATCHED AND t.status = 'active' THEN UPDATE SET t.value = s.value
WHEN MATCHED AND t.status = 'deleted' THEN DELETE
WHEN NOT MATCHED THEN INSERT (id, value) VALUES (s.id, s.value);";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
        Assert.Equal("avoid-merge", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_MultipleMergeStatements_ReturnsMultipleDiagnostics()
    {
        // Arrange
        const string sql = @"
MERGE INTO t1 USING s1 ON t1.id = s1.id WHEN MATCHED THEN UPDATE SET t1.val = s1.val;
MERGE INTO t2 USING s2 ON t2.id = s2.id WHEN MATCHED THEN UPDATE SET t2.val = s2.val;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Equal(2, diagnostics.Length);
        Assert.All(diagnostics, d => Assert.Equal("avoid-merge", d.Code));
    }

    [Fact]
    public void Analyze_UpdateStatement_NoDiagnostic()
    {
        // Arrange
        const string sql = "UPDATE users SET name = 'test' WHERE id = 1;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_InsertStatement_NoDiagnostic()
    {
        // Arrange
        const string sql = "INSERT INTO users (id, name) VALUES (1, 'test');";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_DeleteStatement_NoDiagnostic()
    {
        // Arrange
        const string sql = "DELETE FROM users WHERE id = 1;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_SelectStatement_NoDiagnostic()
    {
        // Arrange
        const string sql = "SELECT * FROM users;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
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
        const string sql = "MERGE INTO t USING s ON t.id = s.id WHEN MATCHED THEN UPDATE SET t.val = s.val;";
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
        Assert.Equal("avoid-merge", _rule.Metadata.RuleId);
        Assert.Equal("Safety", _rule.Metadata.Category);
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
