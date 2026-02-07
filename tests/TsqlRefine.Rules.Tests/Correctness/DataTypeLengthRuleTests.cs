using TsqlRefine.Rules.Rules.Correctness.Semantic;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Correctness;

public sealed class DataTypeLengthRuleTests
{
    private readonly DataTypeLengthRule _rule = new();



    [Fact]
    public void Analyze_VarcharWithLength_NoDiagnostics()
    {
        // Arrange
        var sql = "DECLARE @Name VARCHAR(50);";

        // Act
        var diagnostics = _rule.Analyze(RuleTestContext.CreateContext(sql)).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_VarcharWithoutLength_ReturnsDiagnostic()
    {
        // Arrange
        var sql = "DECLARE @Name VARCHAR;";

        // Act
        var diagnostics = _rule.Analyze(RuleTestContext.CreateContext(sql)).ToArray();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("semantic/data-type-length", diagnostic.Code);
        Assert.Contains("VARCHAR", diagnostic.Message);
    }

    [Fact]
    public void Analyze_NvarcharWithoutLength_ReturnsDiagnostic()
    {
        // Arrange
        var sql = "DECLARE @Name NVARCHAR;";

        // Act
        var diagnostics = _rule.Analyze(RuleTestContext.CreateContext(sql)).ToArray();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("semantic/data-type-length", diagnostic.Code);
        Assert.Contains("NVARCHAR", diagnostic.Message);
    }

    [Fact]
    public void Analyze_CharWithoutLength_ReturnsDiagnostic()
    {
        // Arrange
        var sql = "DECLARE @Code CHAR;";

        // Act
        var diagnostics = _rule.Analyze(RuleTestContext.CreateContext(sql)).ToArray();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("semantic/data-type-length", diagnostic.Code);
        Assert.Contains("CHAR", diagnostic.Message);
    }

    [Fact]
    public void Analyze_NcharWithoutLength_ReturnsDiagnostic()
    {
        // Arrange
        var sql = "DECLARE @Code NCHAR;";

        // Act
        var diagnostics = _rule.Analyze(RuleTestContext.CreateContext(sql)).ToArray();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("semantic/data-type-length", diagnostic.Code);
        Assert.Contains("NCHAR", diagnostic.Message);
    }

    [Fact]
    public void Analyze_VarbinaryWithoutLength_ReturnsDiagnostic()
    {
        // Arrange
        var sql = "DECLARE @Data VARBINARY;";

        // Act
        var diagnostics = _rule.Analyze(RuleTestContext.CreateContext(sql)).ToArray();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("semantic/data-type-length", diagnostic.Code);
        Assert.Contains("VARBINARY", diagnostic.Message);
    }

    [Fact]
    public void Analyze_BinaryWithoutLength_ReturnsDiagnostic()
    {
        // Arrange
        var sql = "DECLARE @Data BINARY;";

        // Act
        var diagnostics = _rule.Analyze(RuleTestContext.CreateContext(sql)).ToArray();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("semantic/data-type-length", diagnostic.Code);
        Assert.Contains("BINARY", diagnostic.Message);
    }

    [Fact]
    public void Analyze_VarcharMaxWithLength_NoDiagnostics()
    {
        // Arrange
        var sql = "DECLARE @Name VARCHAR(MAX);";

        // Act
        var diagnostics = _rule.Analyze(RuleTestContext.CreateContext(sql)).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_IntWithoutLength_NoDiagnostics()
    {
        // Arrange
        var sql = "DECLARE @Id INT;";

        // Act
        var diagnostics = _rule.Analyze(RuleTestContext.CreateContext(sql)).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_DateTimeWithoutLength_NoDiagnostics()
    {
        // Arrange
        var sql = "DECLARE @Created DATETIME;";

        // Act
        var diagnostics = _rule.Analyze(RuleTestContext.CreateContext(sql)).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_ColumnDefinitionWithoutLength_ReturnsDiagnostic()
    {
        // Arrange
        var sql = @"
CREATE TABLE Users (
    Name VARCHAR
);";

        // Act
        var diagnostics = _rule.Analyze(RuleTestContext.CreateContext(sql)).ToArray();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("semantic/data-type-length", diagnostic.Code);
    }

    [Fact]
    public void Analyze_MultipleVariablesWithoutLength_ReturnsMultipleDiagnostics()
    {
        // Arrange
        var sql = @"
DECLARE @Name VARCHAR;
DECLARE @Code CHAR;
DECLARE @Data VARBINARY;";

        // Act
        var diagnostics = _rule.Analyze(RuleTestContext.CreateContext(sql)).ToArray();

        // Assert
        Assert.Equal(3, diagnostics.Length);
        Assert.All(diagnostics, d => Assert.Equal("semantic/data-type-length", d.Code));
    }

    [Fact]
    public void GetFixes_VarcharWithoutLength_ReturnsFix()
    {
        // Arrange
        var sql = "DECLARE @Name VARCHAR;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostic = _rule.Analyze(context).Single();

        // Act
        var fixes = _rule.GetFixes(context, diagnostic).ToArray();

        // Assert
        var fix = Assert.Single(fixes);
        Assert.Equal("Add (50) length specification", fix.Title);
        var edit = Assert.Single(fix.Edits);
        Assert.Equal("(50)", edit.NewText);
    }

    [Fact]
    public void GetFixes_NvarcharWithoutLength_ReturnsFix()
    {
        // Arrange
        var sql = "DECLARE @Name NVARCHAR;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostic = _rule.Analyze(context).Single();

        // Act
        var fixes = _rule.GetFixes(context, diagnostic).ToArray();

        // Assert
        var fix = Assert.Single(fixes);
        Assert.Equal("Add (50) length specification", fix.Title);
        var edit = Assert.Single(fix.Edits);
        Assert.Equal("(50)", edit.NewText);
    }

    [Fact]
    public void GetFixes_CharWithoutLength_ReturnsFix()
    {
        // Arrange
        var sql = "DECLARE @Code CHAR;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostic = _rule.Analyze(context).Single();

        // Act
        var fixes = _rule.GetFixes(context, diagnostic).ToArray();

        // Assert
        var fix = Assert.Single(fixes);
        Assert.Equal("Add (1) length specification", fix.Title);
        var edit = Assert.Single(fix.Edits);
        Assert.Equal("(1)", edit.NewText);
    }

    [Fact]
    public void GetFixes_VarbinaryWithoutLength_ReturnsFix()
    {
        // Arrange
        var sql = "DECLARE @Data VARBINARY;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostic = _rule.Analyze(context).Single();

        // Act
        var fixes = _rule.GetFixes(context, diagnostic).ToArray();

        // Assert
        var fix = Assert.Single(fixes);
        Assert.Equal("Add (50) length specification", fix.Title);
        var edit = Assert.Single(fix.Edits);
        Assert.Equal("(50)", edit.NewText);
    }

    [Fact]
    public void GetFixes_BinaryWithoutLength_ReturnsFix()
    {
        // Arrange
        var sql = "DECLARE @Data BINARY;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostic = _rule.Analyze(context).Single();

        // Act
        var fixes = _rule.GetFixes(context, diagnostic).ToArray();

        // Assert
        var fix = Assert.Single(fixes);
        Assert.Equal("Add (1) length specification", fix.Title);
        var edit = Assert.Single(fix.Edits);
        Assert.Equal("(1)", edit.NewText);
    }

    [Fact]
    public void GetFixes_NcharWithoutLength_ReturnsFix()
    {
        // Arrange
        var sql = "DECLARE @Code NCHAR;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostic = _rule.Analyze(context).Single();

        // Act
        var fixes = _rule.GetFixes(context, diagnostic).ToArray();

        // Assert
        var fix = Assert.Single(fixes);
        Assert.Equal("Add (1) length specification", fix.Title);
        var edit = Assert.Single(fix.Edits);
        Assert.Equal("(1)", edit.NewText);
    }

    [Fact]
    public void GetFixes_MultipleViolations_ReturnsFixesForEach()
    {
        // Arrange
        var sql = @"
DECLARE @Name VARCHAR;
DECLARE @Code CHAR;
DECLARE @Data VARBINARY;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        // Act
        var fixes = diagnostics.Select(d => _rule.GetFixes(context, d).ToArray()).ToArray();

        // Assert
        Assert.Equal(3, fixes.Length);
        Assert.All(fixes, f => Assert.Single(f));
    }

    [Fact]
    public void GetFixes_ColumnDefinitionWithoutLength_ReturnsFix()
    {
        // Arrange
        var sql = @"
CREATE TABLE Users (
    Name VARCHAR
);";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostic = _rule.Analyze(context).Single();

        // Act
        var fixes = _rule.GetFixes(context, diagnostic).ToArray();

        // Assert
        var fix = Assert.Single(fixes);
        Assert.Equal("Add (50) length specification", fix.Title);
    }
}
