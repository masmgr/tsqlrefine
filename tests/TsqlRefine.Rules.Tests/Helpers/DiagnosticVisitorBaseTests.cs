using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

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

    [Fact]
    public void AddDiagnostic_WithSeverity_CreatesDiagnosticWithSeverity()
    {
        // Arrange
        var visitor = new TestVisitorWithSeverity();
        var sql = "SELECT * FROM users";
        var parser = new TSql160Parser(true);
        var fragment = parser.Parse(new System.IO.StringReader(sql), out var errors);

        // Act
        visitor.TestAddDiagnosticWithSeverity(
            fragment,
            "Test message",
            "test-code",
            "Test",
            false,
            PluginSdk.DiagnosticSeverity.Error
        );

        // Assert
        Assert.Single(visitor.Diagnostics);
        var diagnostic = visitor.Diagnostics[0];
        Assert.Equal(PluginSdk.DiagnosticSeverity.Error, diagnostic.Severity);
    }

    [Fact]
    public void AddDiagnostic_WithNullSeverity_CreatesDiagnosticWithNullSeverity()
    {
        // Arrange
        var visitor = new TestVisitorWithSeverity();
        var sql = "SELECT 1";
        var parser = new TSql160Parser(true);
        var fragment = parser.Parse(new System.IO.StringReader(sql), out var errors);

        // Act
        visitor.TestAddDiagnosticWithSeverity(
            fragment,
            "Test message",
            "test-code",
            "Test",
            false,
            null
        );

        // Assert
        Assert.Single(visitor.Diagnostics);
        Assert.Null(visitor.Diagnostics[0].Severity);
    }

    private sealed class TestVisitorWithSeverity : DiagnosticVisitorBase
    {
        public void TestAddDiagnosticWithSeverity(
            TSqlFragment fragment,
            string message,
            string code,
            string category,
            bool fixable,
            PluginSdk.DiagnosticSeverity? severity)
        {
            AddDiagnostic(fragment, message, code, category, fixable, severity);
        }
    }

    private static readonly RuleMetadata TestMetadata = new(
        RuleId: "test-rule",
        Description: "Test rule",
        Category: "Correctness",
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: true
    );

    private sealed class MetadataTestVisitor : DiagnosticVisitorBase
    {
        public void TestAddDiagnostic(
            TSqlFragment fragment,
            string message,
            PluginSdk.DiagnosticSeverity? severity = null,
            bool? fixable = null)
        {
            AddDiagnostic(fragment, message, severity, fixable);
        }

        public void TestAddDiagnostic(TsqlRefine.PluginSdk.Range range, string message)
        {
            AddDiagnostic(range, message);
        }
    }

    private static TSqlFragment ParseFragment(string sql)
    {
        var parser = new TSql160Parser(true);
        return parser.Parse(new StringReader(sql), out _);
    }

    [Fact]
    public void AddDiagnostic_MetadataBased_DerivesCodeCategoryAndFixableFromMetadata()
    {
        var visitor = new MetadataTestVisitor { RuleMetadata = TestMetadata };

        visitor.TestAddDiagnostic(ParseFragment("SELECT 1"), "Test message");

        var diagnostic = Assert.Single(visitor.Diagnostics);
        Assert.Equal("test-rule", diagnostic.Code);
        Assert.NotNull(diagnostic.Data);
        Assert.Equal("test-rule", diagnostic.Data.RuleId);
        Assert.Equal("Correctness", diagnostic.Data.Category);
        Assert.True(diagnostic.Data.Fixable);
        Assert.Null(diagnostic.Severity);
    }

    [Fact]
    public void AddDiagnostic_MetadataBased_WithOverrides_AppliesSeverityAndFixable()
    {
        var visitor = new MetadataTestVisitor { RuleMetadata = TestMetadata };

        visitor.TestAddDiagnostic(
            ParseFragment("SELECT 1"),
            "Test message",
            severity: PluginSdk.DiagnosticSeverity.Error,
            fixable: false);

        var diagnostic = Assert.Single(visitor.Diagnostics);
        Assert.Equal(PluginSdk.DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.False(diagnostic.Data!.Fixable);
    }

    [Fact]
    public void AddDiagnostic_MetadataBased_WithRange_UsesGivenRange()
    {
        var visitor = new MetadataTestVisitor { RuleMetadata = TestMetadata };
        var range = new TsqlRefine.PluginSdk.Range(new Position(1, 2), new Position(1, 5));

        visitor.TestAddDiagnostic(range, "Test message");

        var diagnostic = Assert.Single(visitor.Diagnostics);
        Assert.Equal(range, diagnostic.Range);
        Assert.Equal("test-rule", diagnostic.Code);
    }

    [Fact]
    public void AddDiagnostic_MetadataBased_WithoutMetadata_ThrowsInvalidOperationException()
    {
        var visitor = new MetadataTestVisitor();

        Assert.Throws<InvalidOperationException>(() =>
            visitor.TestAddDiagnostic(ParseFragment("SELECT 1"), "Test message"));
    }

    [Fact]
    public void Analyze_VisitorHasDifferentMetadata_UsesDrivingRuleMetadata()
    {
        const string sql = "SELECT 1";
        var fragment = ParseFragment(sql);
        var context = new RuleContext(
            "test.sql",
            160,
            new ScriptDomAst(sql, fragment, [], []),
            [],
            new RuleSettings());
        var rule = new MetadataInjectionRule();

        var diagnostic = Assert.Single(rule.Analyze(context));

        Assert.Equal(rule.Metadata.RuleId, diagnostic.Code);
        Assert.Equal(rule.Metadata.RuleId, diagnostic.Data!.RuleId);
        Assert.Equal(rule.Metadata.Category, diagnostic.Data.Category);
        Assert.Equal(rule.Metadata.Fixable, diagnostic.Data.Fixable);
    }

    private sealed class MetadataInjectionRule : DiagnosticVisitorRuleBase
    {
        public override RuleMetadata Metadata => TestMetadata;

        protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
            new PreconfiguredMetadataVisitor
            {
                RuleMetadata = new RuleMetadata(
                    "wrong-rule", "Wrong metadata", "Style", RuleSeverity.Error, Fixable: false)
            };
    }

    private sealed class PreconfiguredMetadataVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(TSqlScript node)
        {
            AddDiagnostic(node, "Test message");
        }
    }
}
