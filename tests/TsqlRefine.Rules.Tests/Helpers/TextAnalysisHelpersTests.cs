using TsqlRefine.Rules.Helpers;
using Xunit;

namespace TsqlRefine.Rules.Tests.Helpers;

public sealed class TextAnalysisHelpersTests
{
    #region SplitSqlLines Tests

    [Fact]
    public void SplitSqlLines_WithLF_SplitsCorrectly()
    {
        // Arrange
        var sql = "SELECT 1\nFROM table1\nWHERE id = 1";

        // Act
        var result = TextAnalysisHelpers.SplitSqlLines(sql);

        // Assert
        Assert.Equal(3, result.Length);
        Assert.Equal("SELECT 1", result[0]);
        Assert.Equal("FROM table1", result[1]);
        Assert.Equal("WHERE id = 1", result[2]);
    }

    [Fact]
    public void SplitSqlLines_WithCRLF_SplitsCorrectly()
    {
        // Arrange
        var sql = "SELECT 1\r\nFROM table1\r\nWHERE id = 1";

        // Act
        var result = TextAnalysisHelpers.SplitSqlLines(sql);

        // Assert
        Assert.Equal(3, result.Length);
        Assert.Equal("SELECT 1", result[0]);
        Assert.Equal("FROM table1", result[1]);
        Assert.Equal("WHERE id = 1", result[2]);
    }

    [Fact]
    public void SplitSqlLines_WithCR_SplitsCorrectly()
    {
        // Arrange
        var sql = "SELECT 1\rFROM table1\rWHERE id = 1";

        // Act
        var result = TextAnalysisHelpers.SplitSqlLines(sql);

        // Assert
        Assert.Equal(3, result.Length);
        Assert.Equal("SELECT 1", result[0]);
        Assert.Equal("FROM table1", result[1]);
        Assert.Equal("WHERE id = 1", result[2]);
    }

    [Fact]
    public void SplitSqlLines_WithMixedLineEndings_SplitsCorrectly()
    {
        // Arrange
        var sql = "SELECT 1\r\nFROM table1\nWHERE id = 1\rORDER BY id";

        // Act
        var result = TextAnalysisHelpers.SplitSqlLines(sql);

        // Assert
        Assert.Equal(4, result.Length);
        Assert.Equal("SELECT 1", result[0]);
        Assert.Equal("FROM table1", result[1]);
        Assert.Equal("WHERE id = 1", result[2]);
        Assert.Equal("ORDER BY id", result[3]);
    }

    [Fact]
    public void SplitSqlLines_WithEmptyString_ReturnsEmptyArray()
    {
        // Arrange
        var sql = string.Empty;

        // Act
        var result = TextAnalysisHelpers.SplitSqlLines(sql);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void SplitSqlLines_WithNull_ReturnsEmptyArray()
    {
        // Act
        var result = TextAnalysisHelpers.SplitSqlLines(null);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void SplitSqlLines_WithSingleLine_ReturnsSingleElement()
    {
        // Arrange
        var sql = "SELECT 1";

        // Act
        var result = TextAnalysisHelpers.SplitSqlLines(sql);

        // Assert
        Assert.Single(result);
        Assert.Equal("SELECT 1", result[0]);
    }

    [Fact]
    public void SplitSqlLines_WithEmptyLines_PreservesEmptyLines()
    {
        // Arrange
        var sql = "SELECT 1\n\nFROM table1";

        // Act
        var result = TextAnalysisHelpers.SplitSqlLines(sql);

        // Assert
        Assert.Equal(3, result.Length);
        Assert.Equal("SELECT 1", result[0]);
        Assert.Equal("", result[1]);
        Assert.Equal("FROM table1", result[2]);
    }

    #endregion

    #region CreateLineRangeDiagnostic Tests

    [Fact]
    public void CreateLineRangeDiagnostic_WithValidParameters_CreatesDiagnostic()
    {
        // Act
        var diagnostic = TextAnalysisHelpers.CreateLineRangeDiagnostic(
            lineNumber: 5,
            lineLength: 20,
            message: "Test message",
            code: "test-code",
            category: "Test",
            fixable: false
        );

        // Assert
        Assert.NotNull(diagnostic);
        Assert.Equal(5, diagnostic.Range.Start.Line);
        Assert.Equal(0, diagnostic.Range.Start.Character);
        Assert.Equal(5, diagnostic.Range.End.Line);
        Assert.Equal(20, diagnostic.Range.End.Character);
        Assert.Equal("Test message", diagnostic.Message);
        Assert.Equal("test-code", diagnostic.Code);
        Assert.NotNull(diagnostic.Data);
        Assert.Equal("Test", diagnostic.Data.Category);
        Assert.False(diagnostic.Data.Fixable);
    }

    [Fact]
    public void CreateLineRangeDiagnostic_WithZeroLineNumber_CreatesDiagnostic()
    {
        // Act
        var diagnostic = TextAnalysisHelpers.CreateLineRangeDiagnostic(
            lineNumber: 0,
            lineLength: 10,
            message: "First line",
            code: "test",
            category: "Test",
            fixable: true
        );

        // Assert
        Assert.Equal(0, diagnostic.Range.Start.Line);
        Assert.Equal(0, diagnostic.Range.End.Line);
        Assert.True(diagnostic.Data?.Fixable);
    }

    [Fact]
    public void CreateLineRangeDiagnostic_WithZeroLineLength_CreatesDiagnostic()
    {
        // Act
        var diagnostic = TextAnalysisHelpers.CreateLineRangeDiagnostic(
            lineNumber: 5,
            lineLength: 0,
            message: "Empty line",
            code: "test",
            category: "Test",
            fixable: false
        );

        // Assert
        Assert.Equal(0, diagnostic.Range.Start.Character);
        Assert.Equal(0, diagnostic.Range.End.Character);
    }

    [Fact]
    public void CreateLineRangeDiagnostic_WithNegativeLineNumber_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TextAnalysisHelpers.CreateLineRangeDiagnostic(
                lineNumber: -1,
                lineLength: 10,
                message: "msg",
                code: "code",
                category: "cat",
                fixable: false
            ));
    }

    [Fact]
    public void CreateLineRangeDiagnostic_WithNegativeLineLength_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TextAnalysisHelpers.CreateLineRangeDiagnostic(
                lineNumber: 0,
                lineLength: -1,
                message: "msg",
                code: "code",
                category: "cat",
                fixable: false
            ));
    }

    [Fact]
    public void CreateLineRangeDiagnostic_WithNullMessage_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            TextAnalysisHelpers.CreateLineRangeDiagnostic(
                lineNumber: 0,
                lineLength: 10,
                message: null!,
                code: "code",
                category: "cat",
                fixable: false
            ));
    }

    [Fact]
    public void CreateLineRangeDiagnostic_WithNullCode_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            TextAnalysisHelpers.CreateLineRangeDiagnostic(
                lineNumber: 0,
                lineLength: 10,
                message: "msg",
                code: null!,
                category: "cat",
                fixable: false
            ));
    }

    [Fact]
    public void CreateLineRangeDiagnostic_WithNullCategory_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            TextAnalysisHelpers.CreateLineRangeDiagnostic(
                lineNumber: 0,
                lineLength: 10,
                message: "msg",
                code: "code",
                category: null!,
                fixable: false
            ));
    }

    #endregion
}
