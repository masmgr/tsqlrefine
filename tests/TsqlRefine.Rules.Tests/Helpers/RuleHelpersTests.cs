using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers;
using Xunit;

namespace TsqlRefine.Rules.Tests.Helpers;

public sealed class RuleHelpersTests
{
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
}
