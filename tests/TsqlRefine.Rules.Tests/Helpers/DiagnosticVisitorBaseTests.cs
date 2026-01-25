using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers;
using Xunit;

namespace TsqlRefine.Rules.Tests.Helpers;

public sealed class DiagnosticVisitorBaseTests
{
    private sealed class TestVisitor : DiagnosticVisitorBase
    {
        public void TestAddDiagnostic(Diagnostic diagnostic)
        {
            AddDiagnostic(diagnostic);
        }

        public void TestAddDiagnosticWithFragment(
            TSqlFragment fragment,
            string message,
            string code,
            string category,
            bool fixable)
        {
            AddDiagnostic(fragment, message, code, category, fixable);
        }
    }

    [Fact]
    public void AddDiagnostic_WithValidDiagnostic_AddsToDiagnosticsList()
    {
        // Arrange
        var visitor = new TestVisitor();
        var diagnostic = new Diagnostic(
            Range: new TsqlRefine.PluginSdk.Range(new Position(0, 0), new Position(0, 10)),
            Message: "Test message",
            Code: "test-code",
            Data: new DiagnosticData("test-code", "Test", false)
        );

        // Act
        visitor.TestAddDiagnostic(diagnostic);

        // Assert
        Assert.Single(visitor.Diagnostics);
        Assert.Equal(diagnostic, visitor.Diagnostics[0]);
    }

    [Fact]
    public void AddDiagnostic_WithNullDiagnostic_ThrowsArgumentNullException()
    {
        // Arrange
        var visitor = new TestVisitor();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => visitor.TestAddDiagnostic(null!));
    }

    [Fact]
    public void AddDiagnostic_WithFragment_CreatesDiagnosticCorrectly()
    {
        // Arrange
        var visitor = new TestVisitor();
        var sql = "SELECT * FROM users";
        var parser = new TSql160Parser(true);
        var fragment = parser.Parse(new System.IO.StringReader(sql), out var errors);

        // Act
        visitor.TestAddDiagnosticWithFragment(
            fragment,
            "Test message",
            "test-code",
            "Test",
            false
        );

        // Assert
        Assert.Single(visitor.Diagnostics);
        var diagnostic = visitor.Diagnostics[0];
        Assert.Equal("Test message", diagnostic.Message);
        Assert.Equal("test-code", diagnostic.Code);
        Assert.NotNull(diagnostic.Data);
        Assert.Equal("Test", diagnostic.Data.Category);
        Assert.False(diagnostic.Data.Fixable);
    }

    [Fact]
    public void AddDiagnostic_WithNullFragment_ThrowsArgumentNullException()
    {
        // Arrange
        var visitor = new TestVisitor();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            visitor.TestAddDiagnosticWithFragment(null!, "msg", "code", "cat", false));
    }

    [Fact]
    public void AddDiagnostic_WithNullMessage_ThrowsArgumentNullException()
    {
        // Arrange
        var visitor = new TestVisitor();
        var sql = "SELECT 1";
        var parser = new TSql160Parser(true);
        var fragment = parser.Parse(new System.IO.StringReader(sql), out var errors);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            visitor.TestAddDiagnosticWithFragment(fragment, null!, "code", "cat", false));
    }

    [Fact]
    public void AddDiagnostic_MultipleTimes_AccumulatesDiagnostics()
    {
        // Arrange
        var visitor = new TestVisitor();
        var sql = "SELECT * FROM users";
        var parser = new TSql160Parser(true);
        var fragment = parser.Parse(new System.IO.StringReader(sql), out var errors);

        // Act
        visitor.TestAddDiagnosticWithFragment(fragment, "Message 1", "code1", "Cat1", false);
        visitor.TestAddDiagnosticWithFragment(fragment, "Message 2", "code2", "Cat2", false);
        visitor.TestAddDiagnosticWithFragment(fragment, "Message 3", "code3", "Cat3", false);

        // Assert
        Assert.Equal(3, visitor.Diagnostics.Count);
        Assert.Equal("Message 1", visitor.Diagnostics[0].Message);
        Assert.Equal("Message 2", visitor.Diagnostics[1].Message);
        Assert.Equal("Message 3", visitor.Diagnostics[2].Message);
    }

    [Fact]
    public void Diagnostics_InitiallyEmpty_ReturnsEmptyList()
    {
        // Arrange
        var visitor = new TestVisitor();

        // Act & Assert
        Assert.Empty(visitor.Diagnostics);
    }
}
