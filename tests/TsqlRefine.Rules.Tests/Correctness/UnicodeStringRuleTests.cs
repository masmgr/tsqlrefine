using TsqlRefine.Rules.Rules.Correctness.Semantic;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Correctness;

public sealed class UnicodeStringRuleTests
{
    private readonly UnicodeStringRule _rule = new();



    [Fact]
    public void Analyze_NvarcharWithUnicodeString_NoDiagnostics()
    {
        // Arrange
        var sql = "DECLARE @Name NVARCHAR(50); SET @Name = 'こんにちは';";

        // Act
        var diagnostics = _rule.Analyze(RuleTestContext.CreateContext(sql)).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_VarcharWithAsciiString_NoDiagnostics()
    {
        // Arrange
        var sql = "DECLARE @Name VARCHAR(50); SET @Name = 'Hello';";

        // Act
        var diagnostics = _rule.Analyze(RuleTestContext.CreateContext(sql)).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_VarcharWithUnicodeString_ReturnsDiagnostic()
    {
        // Arrange
        var sql = "DECLARE @Name VARCHAR(50); SET @Name = 'こんにちは';";

        // Act
        var diagnostics = _rule.Analyze(RuleTestContext.CreateContext(sql)).ToArray();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("semantic-unicode-string", diagnostic.Code);
        Assert.Contains("Unicode", diagnostic.Message);
    }

    [Fact]
    public void Analyze_VarcharWithUnicodeLiteral_ReturnsDiagnostic()
    {
        // Arrange - Even with N prefix, VARCHAR can't properly store Unicode
        // The rule warns because VARCHAR is not suitable for Unicode data
        var sql = "DECLARE @Name VARCHAR(50); SET @Name = N'こんにちは';";

        // Act
        var diagnostics = _rule.Analyze(RuleTestContext.CreateContext(sql)).ToArray();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("semantic-unicode-string", diagnostic.Code);
    }

    [Fact]
    public void Analyze_VarcharWithEmojiString_ReturnsDiagnostic()
    {
        // Arrange
        var sql = "DECLARE @Message VARCHAR(100); SET @Message = 'Hello 😊';";

        // Act
        var diagnostics = _rule.Analyze(RuleTestContext.CreateContext(sql)).ToArray();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("semantic-unicode-string", diagnostic.Code);
    }

    [Fact]
    public void Analyze_IntVariable_NoDiagnostics()
    {
        // Arrange
        var sql = "DECLARE @Id INT; SET @Id = 123;";

        // Act
        var diagnostics = _rule.Analyze(RuleTestContext.CreateContext(sql)).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_UntypedLiteralWithUnicode_NoDiagnostics()
    {
        // Arrange - No variable declaration, just a literal in SELECT
        var sql = "SELECT 'こんにちは';";

        // Act
        var diagnostics = _rule.Analyze(RuleTestContext.CreateContext(sql)).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }
}
