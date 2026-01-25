using System.IO;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules;
using Xunit;

namespace TsqlRefine.Rules.Tests;

public sealed class UtcDatetimeRuleTests
{
    private readonly UtcDatetimeRule _rule = new();

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

    [Fact]
    public void Analyze_Getdate_ReturnsDiagnostic()
    {
        // Arrange
        var sql = "SELECT GETDATE();";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("utc-datetime", diagnostic.Code);
        Assert.Contains("GETDATE", diagnostic.Message);
        Assert.Contains("GETUTCDATE", diagnostic.Message);
    }

    [Fact]
    public void Analyze_Sysdatetime_ReturnsDiagnostic()
    {
        // Arrange
        var sql = "SELECT SYSDATETIME();";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("utc-datetime", diagnostic.Code);
        Assert.Contains("SYSDATETIME", diagnostic.Message);
        Assert.Contains("SYSUTCDATETIME", diagnostic.Message);
    }

    [Fact]
    public void Analyze_CurrentTimestamp_ReturnsDiagnostic()
    {
        // Arrange
        var sql = "SELECT CURRENT_TIMESTAMP;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("utc-datetime", diagnostic.Code);
        Assert.Contains("CURRENT_TIMESTAMP", diagnostic.Message);
    }

    [Fact]
    public void Analyze_Sysdatetimeoffset_ReturnsDiagnostic()
    {
        // Arrange
        var sql = "SELECT SYSDATETIMEOFFSET();";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("utc-datetime", diagnostic.Code);
        Assert.Contains("SYSDATETIMEOFFSET", diagnostic.Message);
    }

    [Fact]
    public void Analyze_Getutcdate_NoDiagnostic()
    {
        // Arrange
        var sql = "SELECT GETUTCDATE();";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_Sysutcdatetime_NoDiagnostic()
    {
        // Arrange
        var sql = "SELECT SYSUTCDATETIME();";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_GetdateInInsert_ReturnsDiagnostic()
    {
        // Arrange
        var sql = "INSERT INTO logs (created_at) VALUES (GETDATE());";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("utc-datetime", diagnostic.Code);
    }

    [Fact]
    public void Analyze_GetdateInUpdate_ReturnsDiagnostic()
    {
        // Arrange
        var sql = "UPDATE logs SET updated_at = GETDATE() WHERE id = 1;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("utc-datetime", diagnostic.Code);
    }

    [Fact]
    public void Analyze_GetdateInWhere_ReturnsDiagnostic()
    {
        // Arrange
        var sql = "SELECT * FROM logs WHERE created_at > GETDATE() - 7;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("utc-datetime", diagnostic.Code);
    }

    [Fact]
    public void Analyze_MultipleLocalDatetimeFunctions_ReturnsMultipleDiagnostics()
    {
        // Arrange
        var sql = @"
            SELECT GETDATE() AS local_time,
                   SYSDATETIME() AS local_time2,
                   CURRENT_TIMESTAMP AS local_time3;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        Assert.Equal(3, diagnostics.Count);
        Assert.All(diagnostics, d => Assert.Equal("utc-datetime", d.Code));
    }

    [Fact]
    public void Analyze_GetdateCaseInsensitive_ReturnsDiagnostic()
    {
        // Arrange
        var sql = "SELECT getdate();";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("utc-datetime", diagnostic.Code);
    }

    [Fact]
    public void GetFixes_ReturnsNoFixes()
    {
        // Arrange
        var sql = "SELECT GETDATE();";
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
        Assert.Equal("utc-datetime", _rule.Metadata.RuleId);
        Assert.Equal("Performance", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Warning, _rule.Metadata.DefaultSeverity);
        Assert.False(_rule.Metadata.Fixable);
    }
}
