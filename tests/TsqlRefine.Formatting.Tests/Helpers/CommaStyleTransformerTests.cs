using TsqlRefine.Formatting.Helpers.Transformation;
using Xunit;

namespace TsqlRefine.Formatting.Tests.Helpers;

public class CommaStyleTransformerTests
{
    #region ToLeadingCommas - Basic Cases

    [Fact]
    public void ToLeadingCommas_EmptyString_ReturnsEmpty()
    {
        var result = CommaStyleTransformer.ToLeadingCommas("");
        Assert.Equal("", result);
    }

    [Fact]
    public void ToLeadingCommas_NoCommas_ReturnsUnchanged()
    {
        var input = "SELECT id FROM Users";
        var result = CommaStyleTransformer.ToLeadingCommas(input);
        Assert.Equal(input, result);
    }

    [Fact]
    public void ToLeadingCommas_SingleLine_ReturnsUnchanged()
    {
        var input = "SELECT id, name, email FROM Users";
        var result = CommaStyleTransformer.ToLeadingCommas(input);
        Assert.Equal(input, result);
    }

    #endregion

    #region ToLeadingCommas - Trailing to Leading Conversion

    [Fact]
    public void ToLeadingCommas_BasicConversion_MovesCommaToNextLine()
    {
        var input = "SELECT id,\n    name";
        var result = CommaStyleTransformer.ToLeadingCommas(input);
        // Comma is moved to beginning of next line with a space after it
        Assert.Equal("SELECT id\n    , name", result);
    }

    [Fact]
    public void ToLeadingCommas_MultipleColumns_ConvertsTrailingCommas()
    {
        var input = "SELECT id,\n    name,\n    email";
        var result = CommaStyleTransformer.ToLeadingCommas(input);
        Assert.Equal("SELECT id\n    , name\n    , email", result);
    }

    [Fact]
    public void ToLeadingCommas_PreservesIndentation_MatchesNextLine()
    {
        var input = "SELECT id,\n        name";
        var result = CommaStyleTransformer.ToLeadingCommas(input);
        // Preserves the 8-space indentation
        Assert.Contains("        , name", result);
    }

    #endregion

    #region ToLeadingCommas - Last Line Handling

    [Fact]
    public void ToLeadingCommas_LastLineWithComma_KeepsTrailingComma()
    {
        // Single line ending with comma - no next line to move to
        var input = "SELECT id,";
        var result = CommaStyleTransformer.ToLeadingCommas(input);
        Assert.Equal("SELECT id,", result);
    }

    [Fact]
    public void ToLeadingCommas_LastLineNoFollowingContent_KeepsComma()
    {
        var input = "SELECT id,\nname,";
        var result = CommaStyleTransformer.ToLeadingCommas(input);
        // First comma moves to second line, but last comma has no next line
        Assert.Equal("SELECT id\n, name,", result);
    }

    #endregion

    #region ToLeadingCommas - Whitespace Handling

    [Fact]
    public void ToLeadingCommas_TrailingSpacesBeforeComma_TrimsCorrectly()
    {
        var input = "SELECT id   ,\n    name";
        var result = CommaStyleTransformer.ToLeadingCommas(input);
        // Trailing spaces before comma are trimmed
        Assert.Equal("SELECT id\n    , name", result);
    }

    [Fact]
    public void ToLeadingCommas_TrailingSpacesAfterComma_TrimsCorrectly()
    {
        var input = "SELECT id,   \n    name";
        var result = CommaStyleTransformer.ToLeadingCommas(input);
        Assert.Equal("SELECT id\n    , name", result);
    }

    [Fact]
    public void ToLeadingCommas_TabIndentation_Preserves()
    {
        var input = "SELECT id,\n\tname";
        var result = CommaStyleTransformer.ToLeadingCommas(input);
        Assert.Equal("SELECT id\n\t, name", result);
    }

    #endregion

    #region ToLeadingCommas - Complex Cases

    [Fact]
    public void ToLeadingCommas_MultiClause_ConvertsTrailingCommas()
    {
        var input = "SELECT id,\n    name,\n    email\nFROM Users\nWHERE active = 1";
        var result = CommaStyleTransformer.ToLeadingCommas(input);
        Assert.Contains(", name", result);
        Assert.Contains(", email", result);
        Assert.DoesNotContain("id,", result); // id's comma was moved
        Assert.DoesNotContain("name,", result); // name's comma was also moved
        // FROM and WHERE should remain unchanged
        Assert.Contains("FROM Users", result);
    }

    [Fact]
    public void ToLeadingCommas_NoNextLineContent_HandlesGracefully()
    {
        var input = "SELECT id,\n";
        var result = CommaStyleTransformer.ToLeadingCommas(input);
        // Empty next line gets the comma
        Assert.Contains(",", result);
    }

    #endregion

    #region ToLeadingCommas - Edge Cases

    [Fact]
    public void ToLeadingCommas_ConsecutiveCommas_HandlesCorrectly()
    {
        var input = "SELECT id,,\n    name";
        var result = CommaStyleTransformer.ToLeadingCommas(input);
        // Both commas at end of line - last one triggers the transformation
        Assert.Contains(",", result);
    }

    [Fact]
    public void ToLeadingCommas_EmptyLineAfterComma_HandlesCorrectly()
    {
        var input = "SELECT id,\n\nname";
        var result = CommaStyleTransformer.ToLeadingCommas(input);
        // Empty line gets the comma prepended (no space before content since line is empty)
        Assert.Equal("SELECT id\n,\nname", result);
    }

    [Fact]
    public void ToLeadingCommas_OnlyWhitespaceNextLine_HandlesCorrectly()
    {
        var input = "SELECT id,\n    ";
        var result = CommaStyleTransformer.ToLeadingCommas(input);
        Assert.Contains(",", result);
    }

    #endregion

    #region ToLeadingCommas - Protected Regions

    [Fact]
    public void ToLeadingCommas_CommaInsideString_PreservesString()
    {
        // Comma inside string should not trigger transformation
        var input = "SELECT 'a,b',\n    name";
        var result = CommaStyleTransformer.ToLeadingCommas(input);
        // The trailing comma after string triggers transformation, but string content is preserved
        Assert.Equal("SELECT 'a,b'\n    , name", result);
    }

    [Fact]
    public void ToLeadingCommas_StringEndsWithComma_PreservesStringContent()
    {
        // Line ends with a string containing comma - should not transform the comma inside
        var input = "SELECT 'abc,'\nFROM Users";
        var result = CommaStyleTransformer.ToLeadingCommas(input);
        // No trailing comma outside string, so no transformation
        Assert.Equal(input, result);
    }

    [Fact]
    public void ToLeadingCommas_CommaInsideBlockComment_NotTransformed()
    {
        // Comma inside block comment should not be transformed
        var input = "SELECT id /* comma here, */,\n    name";
        var result = CommaStyleTransformer.ToLeadingCommas(input);
        // The trailing comma after comment triggers transformation
        Assert.Contains(", name", result);
        Assert.Contains("/* comma here, */", result); // Comment content preserved
    }

    [Fact]
    public void ToLeadingCommas_CrlfInput_PreservesCrlf()
    {
        var input = "SELECT id,\r\n    name,\r\n    email";
        var result = CommaStyleTransformer.ToLeadingCommas(input);
        Assert.Equal("SELECT id\r\n    , name\r\n    , email", result);
    }

    [Fact]
    public void ToLeadingCommas_CommaInsideLineComment_NotTransformed()
    {
        // Line comment makes the trailing comma part of comment
        var input = "SELECT id -- comment,\n    name";
        var result = CommaStyleTransformer.ToLeadingCommas(input);
        // Comma is part of comment, so no transformation happens
        Assert.Equal(input, result);
    }

    [Fact]
    public void ToLeadingCommas_CommaInsideBracketIdentifier_PreservesIdentifier()
    {
        // Comma inside bracket identifier should not trigger transformation
        var input = "SELECT [Column,Name],\n    id";
        var result = CommaStyleTransformer.ToLeadingCommas(input);
        // Trailing comma after bracket identifier triggers transformation
        Assert.Equal("SELECT [Column,Name]\n    , id", result);
    }

    [Fact]
    public void ToLeadingCommas_MultilineString_TracksStateCorrectly()
    {
        // String spanning multiple lines - state should be tracked
        var input = "SELECT 'first line,\nsecond line',\n    id";
        var result = CommaStyleTransformer.ToLeadingCommas(input);
        // The comma inside the string should not be transformed
        // Only the trailing comma after the string should transform
        Assert.Contains("'first line,", result);
        Assert.Contains("second line'", result);
    }

    #endregion

    #region ToTrailingCommas - Not Implemented

    [Fact]
    public void ToTrailingCommas_ThrowsNotImplementedException()
    {
        Assert.Throws<NotImplementedException>(() => CommaStyleTransformer.ToTrailingCommas("SELECT id"));
    }

    [Fact]
    public void ToTrailingCommas_AnyInput_ThrowsNotImplementedException()
    {
        var input = ", id\n, name\n, email";
        Assert.Throws<NotImplementedException>(() => CommaStyleTransformer.ToTrailingCommas(input));
    }

    #endregion
}
