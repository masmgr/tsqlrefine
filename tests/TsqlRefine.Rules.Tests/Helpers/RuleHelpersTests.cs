using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers;
using Xunit;

namespace TsqlRefine.Rules.Tests.Helpers;

public sealed class RuleHelpersTests
{
    #region NoFixes Tests

    [Fact]
    public void NoFixes_WithValidArguments_ReturnsEmptyCollection()
    {
        // Arrange
        var context = new RuleContext(
            FilePath: "test.sql",
            CompatLevel: 150,
            Ast: new ScriptDomAst("SELECT 1"),
            Tokens: Array.Empty<Token>(),
            Settings: new RuleSettings()
        );
        var diagnostic = new Diagnostic(
            Range: new TsqlRefine.PluginSdk.Range(new Position(0, 0), new Position(0, 10)),
            Message: "Test",
            Code: "test",
            Data: new DiagnosticData("test", "Test", false)
        );

        // Act
        var result = RuleHelpers.NoFixes(context, diagnostic);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void NoFixes_WithNullContext_ThrowsArgumentNullException()
    {
        // Arrange
        var diagnostic = new Diagnostic(
            Range: new TsqlRefine.PluginSdk.Range(new Position(0, 0), new Position(0, 10)),
            Message: "Test",
            Code: "test",
            Data: new DiagnosticData("test", "Test", false)
        );

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => RuleHelpers.NoFixes(null!, diagnostic));
    }

    [Fact]
    public void NoFixes_WithNullDiagnostic_ThrowsArgumentNullException()
    {
        // Arrange
        var context = new RuleContext(
            FilePath: "test.sql",
            CompatLevel: 150,
            Ast: new ScriptDomAst("SELECT 1"),
            Tokens: Array.Empty<Token>(),
            Settings: new RuleSettings()
        );

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => RuleHelpers.NoFixes(context, null!));
    }

    #endregion

    #region CreateDiagnostic Tests

    [Fact]
    public void CreateDiagnostic_WithValidParameters_CreatesDiagnostic()
    {
        // Arrange
        var range = new TsqlRefine.PluginSdk.Range(new Position(0, 0), new Position(0, 10));

        // Act
        var diagnostic = RuleHelpers.CreateDiagnostic(
            range: range,
            message: "Test message",
            code: "test-code",
            category: "Test",
            fixable: true
        );

        // Assert
        Assert.NotNull(diagnostic);
        Assert.Equal(range, diagnostic.Range);
        Assert.Equal("Test message", diagnostic.Message);
        Assert.Equal("test-code", diagnostic.Code);
        Assert.Null(diagnostic.Severity);
        Assert.NotNull(diagnostic.Data);
        Assert.Equal("test-code", diagnostic.Data.RuleId);
        Assert.Equal("Test", diagnostic.Data.Category);
        Assert.True(diagnostic.Data.Fixable);
    }

    [Fact]
    public void CreateDiagnostic_WithNullRange_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            RuleHelpers.CreateDiagnostic(
                range: null!,
                message: "msg",
                code: "code",
                category: "cat",
                fixable: false
            ));
    }

    [Fact]
    public void CreateDiagnostic_WithNullMessage_ThrowsArgumentNullException()
    {
        // Arrange
        var range = new TsqlRefine.PluginSdk.Range(new Position(0, 0), new Position(0, 10));

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            RuleHelpers.CreateDiagnostic(
                range: range,
                message: null!,
                code: "code",
                category: "cat",
                fixable: false
            ));
    }

    [Fact]
    public void CreateDiagnostic_WithNullCode_ThrowsArgumentNullException()
    {
        // Arrange
        var range = new TsqlRefine.PluginSdk.Range(new Position(0, 0), new Position(0, 10));

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            RuleHelpers.CreateDiagnostic(
                range: range,
                message: "msg",
                code: null!,
                category: "cat",
                fixable: false
            ));
    }

    [Fact]
    public void CreateDiagnostic_WithNullCategory_ThrowsArgumentNullException()
    {
        // Arrange
        var range = new TsqlRefine.PluginSdk.Range(new Position(0, 0), new Position(0, 10));

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            RuleHelpers.CreateDiagnostic(
                range: range,
                message: "msg",
                code: "code",
                category: null!,
                fixable: false
            ));
    }

    #endregion

    #region CreateDiagnosticFromTokens Tests

    [Fact]
    public void CreateDiagnosticFromTokens_WithValidParameters_CreatesDiagnostic()
    {
        // Arrange
        var tokens = new List<Token>
        {
            new Token("SELECT", new Position(0, 0), 6),
            new Token(" ", new Position(0, 6), 1),
            new Token("*", new Position(0, 7), 1),
            new Token(" ", new Position(0, 8), 1),
            new Token("FROM", new Position(0, 9), 4)
        };

        // Act
        var diagnostic = RuleHelpers.CreateDiagnosticFromTokens(
            tokens: tokens,
            startIndex: 0,
            endIndex: 2,
            message: "Test message",
            code: "test-code",
            category: "Test",
            fixable: false
        );

        // Assert
        Assert.NotNull(diagnostic);
        Assert.Equal(0, diagnostic.Range.Start.Line);
        Assert.Equal(0, diagnostic.Range.Start.Character);
        Assert.Equal(0, diagnostic.Range.End.Line);
        Assert.Equal(8, diagnostic.Range.End.Character);
        Assert.Equal("Test message", diagnostic.Message);
        Assert.Equal("test-code", diagnostic.Code);
    }

    [Fact]
    public void CreateDiagnosticFromTokens_WithSingleToken_CreatesDiagnostic()
    {
        // Arrange
        var tokens = new List<Token>
        {
            new Token("SELECT", new Position(0, 0), 6)
        };

        // Act
        var diagnostic = RuleHelpers.CreateDiagnosticFromTokens(
            tokens: tokens,
            startIndex: 0,
            endIndex: 0,
            message: "msg",
            code: "code",
            category: "cat",
            fixable: true
        );

        // Assert
        Assert.Equal(0, diagnostic.Range.Start.Character);
        Assert.Equal(6, diagnostic.Range.End.Character);
    }

    [Fact]
    public void CreateDiagnosticFromTokens_WithNullTokens_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            RuleHelpers.CreateDiagnosticFromTokens(
                tokens: null!,
                startIndex: 0,
                endIndex: 0,
                message: "msg",
                code: "code",
                category: "cat",
                fixable: false
            ));
    }

    [Fact]
    public void CreateDiagnosticFromTokens_WithNegativeStartIndex_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var tokens = new List<Token> { new Token("SELECT", new Position(0, 0), 6) };

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RuleHelpers.CreateDiagnosticFromTokens(
                tokens: tokens,
                startIndex: -1,
                endIndex: 0,
                message: "msg",
                code: "code",
                category: "cat",
                fixable: false
            ));
    }

    [Fact]
    public void CreateDiagnosticFromTokens_WithStartIndexOutOfRange_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var tokens = new List<Token> { new Token("SELECT", new Position(0, 0), 6) };

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RuleHelpers.CreateDiagnosticFromTokens(
                tokens: tokens,
                startIndex: 5,
                endIndex: 5,
                message: "msg",
                code: "code",
                category: "cat",
                fixable: false
            ));
    }

    [Fact]
    public void CreateDiagnosticFromTokens_WithEndIndexOutOfRange_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var tokens = new List<Token> { new Token("SELECT", new Position(0, 0), 6) };

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RuleHelpers.CreateDiagnosticFromTokens(
                tokens: tokens,
                startIndex: 0,
                endIndex: 5,
                message: "msg",
                code: "code",
                category: "cat",
                fixable: false
            ));
    }

    [Fact]
    public void CreateDiagnosticFromTokens_WithStartGreaterThanEnd_ThrowsArgumentException()
    {
        // Arrange
        var tokens = new List<Token>
        {
            new Token("SELECT", new Position(0, 0), 6),
            new Token("*", new Position(0, 7), 1)
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            RuleHelpers.CreateDiagnosticFromTokens(
                tokens: tokens,
                startIndex: 1,
                endIndex: 0,
                message: "msg",
                code: "code",
                category: "cat",
                fixable: false
            ));
    }

    #endregion
}
