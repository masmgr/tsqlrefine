using TsqlRefine.Formatting;
using TsqlRefine.Formatting.Helpers;
using Xunit;

namespace TsqlRefine.Formatting.Tests.Helpers;

public class InlineSpaceNormalizerTests
{
    private readonly FormattingOptions _defaultOptions = new();

    [Fact]
    public void Normalize_EmptyString_ReturnsEmpty()
    {
        var result = InlineSpaceNormalizer.Normalize("", _defaultOptions);
        Assert.Equal("", result);
    }

    [Fact]
    public void Normalize_NullString_ReturnsNull()
    {
        var result = InlineSpaceNormalizer.Normalize(null!, _defaultOptions);
        Assert.Null(result);
    }

    [Fact]
    public void Normalize_OnlySpaces_HandlesGracefully()
    {
        // A line with only spaces is treated as leading whitespace and preserved
        var result = InlineSpaceNormalizer.Normalize("   ", _defaultOptions);
        Assert.Equal("   ", result);
    }

    [Fact]
    public void Normalize_BasicCommaSpacing_AddsSpaceAfterComma()
    {
        var input = "SELECT id,name,email";
        var result = InlineSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal("SELECT id, name, email", result);
    }

    [Fact]
    public void Normalize_DuplicateSpaces_PreservesOriginal()
    {
        var input = "SELECT  id,  name";
        var result = InlineSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal("SELECT  id,  name", result);
    }

    [Fact]
    public void Normalize_TripleSpaces_PreservesOriginal()
    {
        var input = "SELECT   id   FROM   users";
        var result = InlineSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal("SELECT   id   FROM   users", result);
    }

    [Fact]
    public void Normalize_CommaWithExistingSpace_Unchanged()
    {
        var input = "SELECT id, name";
        var result = InlineSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal("SELECT id, name", result);
    }

    [Fact]
    public void Normalize_CommaAtEndOfLine_NoSpaceAdded()
    {
        var input = "SELECT id,";
        var result = InlineSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal("SELECT id,", result);
    }

    [Fact]
    public void Normalize_MultipleConsecutiveCommas_SpaceAfterEach()
    {
        var input = "SELECT id,,name";
        var result = InlineSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal("SELECT id,, name", result);
    }

    [Fact]
    public void Normalize_PreserveStringLiterals_NoChange()
    {
        var input = "SELECT 'a,b',  'test  string'";
        var result = InlineSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal("SELECT 'a,b',  'test  string'", result);
    }

    [Fact]
    public void Normalize_PreserveLineComments_NoChange()
    {
        var input = "SELECT id,  name  -- test,  comment";
        var result = InlineSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal("SELECT id,  name  -- test,  comment", result);
    }

    [Fact]
    public void Normalize_PreserveBlockComments_NoChange()
    {
        var input = "SELECT id,  /* test,  comment */  name";
        var result = InlineSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal("SELECT id,  /* test,  comment */  name", result);
    }

    [Fact]
    public void Normalize_PreserveBrackets_NoChange()
    {
        var input = "SELECT [Column,Name],  id";
        var result = InlineSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal("SELECT [Column,Name],  id", result);
    }

    [Fact]
    public void Normalize_PreserveDoubleQuotedIdentifiers_NoChange()
    {
        var input = "SELECT \"Column,Name\",  id";
        var result = InlineSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal("SELECT \"Column,Name\",  id", result);
    }

    [Fact]
    public void Normalize_EscapedQuotes_PreservesContent()
    {
        var input = "SELECT 'test''s  value',  id";
        var result = InlineSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal("SELECT 'test''s  value',  id", result);
    }

    [Fact]
    public void Normalize_EscapedBrackets_PreservesContent()
    {
        var input = "SELECT [Table]]Name],  id";
        var result = InlineSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal("SELECT [Table]]Name],  id", result);
    }

    [Fact]
    public void Normalize_TabCharacters_PreservesDuplicates()
    {
        var input = "SELECT\tid,\t\tname";
        var result = InlineSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal("SELECT\tid,\t\tname", result);
    }

    [Fact]
    public void Normalize_MixedSpacesAndTabs_PreservesDuplicates()
    {
        var input = "SELECT \t id, \t \tname";
        var result = InlineSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal("SELECT \t id, \t \tname", result);
    }

    [Fact]
    public void Normalize_ComplexQuery_PreservesSpacing()
    {
        var input = @"SELECT  o.OrderId,  o.OrderDate,  c.CustomerName
FROM  dbo.Orders  o
INNER  JOIN  dbo.Customers  c  ON  o.CustomerId  =  c.CustomerId
WHERE  o.OrderDate  >=  '2024-01-01'";

        var result = InlineSpaceNormalizer.Normalize(input, _defaultOptions);

        // Spaces are preserved, only comma spacing is normalized
        var expected = @"SELECT  o.OrderId,  o.OrderDate,  c.CustomerName
FROM  dbo.Orders  o
INNER  JOIN  dbo.Customers  c  ON  o.CustomerId  =  c.CustomerId
WHERE  o.OrderDate  >=  '2024-01-01'";

        Assert.Equal(expected, result);
    }

    [Fact]
    public void Normalize_DisabledOption_ReturnsUnchanged()
    {
        var input = "SELECT  id,name  FROM  users";
        var options = new FormattingOptions { NormalizeInlineSpacing = false };
        var result = InlineSpaceNormalizer.Normalize(input, options);
        Assert.Equal(input, result);
    }

    [Fact]
    public void Normalize_MultilineQuery_PreservesLineBreaks()
    {
        var input = "SELECT id,name\nFROM users\nWHERE active = 1";
        var result = InlineSpaceNormalizer.Normalize(input, _defaultOptions);
        var expected = "SELECT id, name\nFROM users\nWHERE active = 1";
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Normalize_WindowsLineEndings_PreservesLineBreaks()
    {
        var input = "SELECT id,name\r\nFROM users";
        var result = InlineSpaceNormalizer.Normalize(input, _defaultOptions);
        var expected = "SELECT id, name\r\nFROM users";
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Normalize_EmptyProtectedRegions_HandlesCorrectly()
    {
        var input = "SELECT '',  \"\",  [],  /**/, id";
        var result = InlineSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal("SELECT '',  \"\",  [],  /**/, id", result);
    }

    [Fact]
    public void Normalize_SpaceBeforeComma_PreservesAndAddsAfter()
    {
        var input = "SELECT id ,name";
        var result = InlineSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal("SELECT id, name", result);
    }

    [Fact]
    public void Normalize_CommaInMiddleOfProtectedRegion_NoSpaceAdded()
    {
        var input = "SELECT /* before,after */ id";
        var result = InlineSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal("SELECT /* before,after */ id", result);
    }

    [Fact]
    public void Normalize_MultipleBlockComments_AllPreserved()
    {
        var input = "SELECT /* a,  b */  id,  /* c,  d */  name";
        var result = InlineSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal("SELECT /* a,  b */  id,  /* c,  d */  name", result);
    }

    [Fact]
    public void Normalize_NestedQuotedIdentifiers_HandlesCorrectly()
    {
        var input = "SELECT [dbo].[Users],  \"schema\".\"table\"";
        var result = InlineSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal("SELECT [dbo].[Users],  \"schema\".\"table\"", result);
    }

    [Fact]
    public void Normalize_CommaFollowedByMultipleSpaces_PreservesOriginal()
    {
        var input = "SELECT id,    name";
        var result = InlineSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal("SELECT id,    name", result);
    }
}
