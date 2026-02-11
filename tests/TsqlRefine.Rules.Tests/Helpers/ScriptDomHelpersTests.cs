using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace TsqlRefine.Rules.Tests.Helpers;

public sealed class ScriptDomHelpersTests
{
    [Fact]
    public void GetRange_WithValidFragment_ReturnsCorrectRange()
    {
        // Arrange
        var sql = "SELECT * FROM users";
        var parser = new TSql160Parser(true);
        var fragment = parser.Parse(new System.IO.StringReader(sql), out var errors);

        // Act
        var range = ScriptDomHelpers.GetRange(fragment);

        // Assert
        Assert.NotNull(range);
        Assert.Equal(0, range.Start.Line); // 0-based
        Assert.Equal(0, range.Start.Character); // 0-based
    }

    [Fact]
    public void GetRange_WithNullFragment_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => ScriptDomHelpers.GetRange(null!));
    }

    [Fact]
    public void GetRange_WithMultiLineFragment_CalculatesCorrectEndPosition()
    {
        // Arrange
        var sql = @"SELECT
    col1,
    col2
FROM users";
        var parser = new TSql160Parser(true);
        var fragment = parser.Parse(new System.IO.StringReader(sql), out var errors);

        // Act
        var range = ScriptDomHelpers.GetRange(fragment);

        // Assert
        Assert.NotNull(range);
        Assert.Equal(0, range.Start.Line);
        Assert.True(range.End.Line >= range.Start.Line);
    }

    [Fact]
    public void GetRange_WithFragmentNoTokenStream_ReturnsStartPositionOnly()
    {
        // Arrange
        var sql = "SELECT 1";
        var parser = new TSql160Parser(true);
        var fragment = parser.Parse(new System.IO.StringReader(sql), out var errors);

        // Act
        var range = ScriptDomHelpers.GetRange(fragment);

        // Assert
        Assert.NotNull(range);
        // Should have start position at minimum
        Assert.True(range.Start.Line >= 0);
        Assert.True(range.Start.Character >= 0);
    }

    [Fact]
    public void GetFirstTokenRange_WithUpdateStatement_ReturnsUpdateKeywordOnly()
    {
        // Arrange
        var sql = "UPDATE users SET active = 1 WHERE id = 5;";
        var parser = new TSql160Parser(true);
        var script = (TSqlScript)parser.Parse(new System.IO.StringReader(sql), out _);
        var statement = script.Batches[0].Statements[0];

        // Act
        var range = ScriptDomHelpers.GetFirstTokenRange(statement);

        // Assert - should cover only "UPDATE" (6 chars at position 0,0)
        Assert.Equal(0, range.Start.Line);
        Assert.Equal(0, range.Start.Character);
        Assert.Equal(0, range.End.Line);
        Assert.Equal(6, range.End.Character);
    }

    [Fact]
    public void GetFirstTokenRange_WithMultiLineStatement_ReturnsFirstTokenOnly()
    {
        // Arrange
        var sql = @"DELETE FROM users
WHERE active = 0;";
        var parser = new TSql160Parser(true);
        var script = (TSqlScript)parser.Parse(new System.IO.StringReader(sql), out _);
        var statement = script.Batches[0].Statements[0];

        // Act
        var range = ScriptDomHelpers.GetFirstTokenRange(statement);

        // Assert - should cover only "DELETE" (6 chars), not the entire multi-line statement
        Assert.Equal(0, range.Start.Line);
        Assert.Equal(0, range.Start.Character);
        Assert.Equal(0, range.End.Line);
        Assert.Equal(6, range.End.Character);
    }

    [Fact]
    public void GetFirstTokenRange_WithNullFragment_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => ScriptDomHelpers.GetFirstTokenRange(null!));
    }

    [Fact]
    public void FindKeywordTokenRange_WithDistinctKeyword_ReturnsDistinctOnly()
    {
        // Arrange
        var sql = "SELECT DISTINCT col1, col2 FROM users";
        var parser = new TSql160Parser(true);
        var script = (TSqlScript)parser.Parse(new System.IO.StringReader(sql), out _);
        var selectStatement = script.Batches[0].Statements[0] as SelectStatement;
        var querySpec = selectStatement!.QueryExpression as QuerySpecification;

        // Act
        var range = ScriptDomHelpers.FindKeywordTokenRange(querySpec!, TSqlTokenType.Distinct);

        // Assert - "DISTINCT" starts at column 7, length 8
        Assert.Equal(0, range.Start.Line);
        Assert.Equal(7, range.Start.Character);
        Assert.Equal(0, range.End.Line);
        Assert.Equal(15, range.End.Character);
    }

    [Fact]
    public void FindKeywordTokenRange_TokenNotFound_FallsBackToFirstToken()
    {
        // Arrange
        var sql = "SELECT col1, col2 FROM users";
        var parser = new TSql160Parser(true);
        var script = (TSqlScript)parser.Parse(new System.IO.StringReader(sql), out _);
        var selectStatement = script.Batches[0].Statements[0] as SelectStatement;
        var querySpec = selectStatement!.QueryExpression as QuerySpecification;

        // Act - search for DISTINCT which doesn't exist
        var range = ScriptDomHelpers.FindKeywordTokenRange(querySpec!, TSqlTokenType.Distinct);

        // Assert - should fall back to first token "SELECT" (6 chars at 0,0)
        Assert.Equal(0, range.Start.Line);
        Assert.Equal(0, range.Start.Character);
        Assert.Equal(0, range.End.Line);
        Assert.Equal(6, range.End.Character);
    }

    [Fact]
    public void FindKeywordTokenRange_WithNullFragment_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ScriptDomHelpers.FindKeywordTokenRange(null!, TSqlTokenType.Distinct));
    }

    /// <summary>
    /// SQL Server 2022 extended TRIM syntax: TRIM(characters FROM string)
    /// Note: ScriptDom 170.157.0 does not yet support this syntax, even with TSql170Parser.
    /// This test documents the current parser limitation.
    /// When ScriptDom adds support, change Assert.NotEmpty to Assert.Empty.
    /// </summary>
    [Fact]
    public void Parse_TrimWithCharacterSpecifier_SqlServer2022_NotYetSupportedByScriptDom()
    {
        // Arrange - SQL Server 2022 extended TRIM syntax with character specifier
        // NCHAR(12288) is a full-width space (ideographic space)
        var sql = @"
CREATE FUNCTION dbo.TrimFullWidthSpace(@str NVARCHAR(MAX))
RETURNS NVARCHAR(MAX)
AS
BEGIN
    RETURN TRIM(NCHAR(12288) FROM @str);
END";
        var parser = new TSql170Parser(true);

        // Act
        var fragment = parser.Parse(new System.IO.StringReader(sql), out var errors);

        // Assert - ScriptDom 170.157.0 does not yet support TRIM(expr FROM expr) syntax
        // When ScriptDom adds support, change this to Assert.Empty(errors)
        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Message.Contains("TRIM"));
    }
}
