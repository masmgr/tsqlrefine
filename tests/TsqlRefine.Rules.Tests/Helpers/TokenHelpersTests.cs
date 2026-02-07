using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Tests.Helpers;

public sealed class TokenHelpersTests
{
    [Fact]
    public void IsKeyword_WithMatchingKeyword_ReturnsTrue()
    {
        // Arrange
        var token = new Token("SELECT", new Position(0, 0), 6);

        // Act
        var result = TokenHelpers.IsKeyword(token, "select");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsKeyword_WithDifferentCase_ReturnsTrue()
    {
        // Arrange
        var token = new Token("select", new Position(0, 0), 6);

        // Act
        var result = TokenHelpers.IsKeyword(token, "SELECT");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsKeyword_WithNonMatchingKeyword_ReturnsFalse()
    {
        // Arrange
        var token = new Token("FROM", new Position(0, 0), 4);

        // Act
        var result = TokenHelpers.IsKeyword(token, "select");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsKeyword_WithNullToken_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => TokenHelpers.IsKeyword(null!, "select"));
    }

    [Fact]
    public void IsKeyword_WithNullKeyword_ThrowsArgumentNullException()
    {
        // Arrange
        var token = new Token("SELECT", new Position(0, 0), 6);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => TokenHelpers.IsKeyword(token, null!));
    }

    [Fact]
    public void IsTrivia_WithWhitespace_ReturnsTrue()
    {
        // Arrange
        var token = new Token("   ", new Position(0, 0), 3);

        // Act
        var result = TokenHelpers.IsTrivia(token);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsTrivia_WithSingleLineComment_ReturnsTrue()
    {
        // Arrange
        var token = new Token("-- comment", new Position(0, 0), 10);

        // Act
        var result = TokenHelpers.IsTrivia(token);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsTrivia_WithMultiLineComment_ReturnsTrue()
    {
        // Arrange
        var token = new Token("/* comment */", new Position(0, 0), 13);

        // Act
        var result = TokenHelpers.IsTrivia(token);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsTrivia_WithNonTrivia_ReturnsFalse()
    {
        // Arrange
        var token = new Token("SELECT", new Position(0, 0), 6);

        // Act
        var result = TokenHelpers.IsTrivia(token);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsTrivia_WithNullToken_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => TokenHelpers.IsTrivia(null!));
    }

    [Fact]
    public void IsPrefixedByDot_WithDotPrefix_ReturnsTrue()
    {
        // Arrange
        var tokens = new List<Token>
        {
            new Token("table", new Position(0, 0), 5),
            new Token(".", new Position(0, 5), 1),
            new Token("column", new Position(0, 6), 6)
        };

        // Act
        var result = TokenHelpers.IsPrefixedByDot(tokens, 2);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsPrefixedByDot_WithDotPrefixAndTrivia_ReturnsTrue()
    {
        // Arrange
        var tokens = new List<Token>
        {
            new Token("table", new Position(0, 0), 5),
            new Token(".", new Position(0, 5), 1),
            new Token("  ", new Position(0, 6), 2),
            new Token("column", new Position(0, 8), 6)
        };

        // Act
        var result = TokenHelpers.IsPrefixedByDot(tokens, 3);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsPrefixedByDot_WithoutDotPrefix_ReturnsFalse()
    {
        // Arrange
        var tokens = new List<Token>
        {
            new Token("SELECT", new Position(0, 0), 6),
            new Token("column", new Position(0, 7), 6)
        };

        // Act
        var result = TokenHelpers.IsPrefixedByDot(tokens, 1);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsPrefixedByDot_WithNullTokens_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => TokenHelpers.IsPrefixedByDot(null!, 0));
    }

    [Fact]
    public void GetTokenEnd_WithSingleLineToken_ReturnsCorrectPosition()
    {
        // Arrange
        var token = new Token("SELECT", new Position(0, 0), 6);

        // Act
        var result = TokenHelpers.GetTokenEnd(token);

        // Assert
        Assert.Equal(0, result.Line);
        Assert.Equal(6, result.Character);
    }

    [Fact]
    public void GetTokenEnd_WithMultiLineToken_ReturnsCorrectPosition()
    {
        // Arrange
        var token = new Token("line1\nline2", new Position(0, 0), 11);

        // Act
        var result = TokenHelpers.GetTokenEnd(token);

        // Assert
        Assert.Equal(1, result.Line);
        Assert.Equal(5, result.Character);
    }

    [Fact]
    public void GetTokenEnd_WithCRLFLineEnding_ReturnsCorrectPosition()
    {
        // Arrange
        var token = new Token("line1\r\nline2", new Position(0, 0), 12);

        // Act
        var result = TokenHelpers.GetTokenEnd(token);

        // Assert
        Assert.Equal(1, result.Line);
        Assert.Equal(5, result.Character);
    }

    [Fact]
    public void GetTokenEnd_WithNullToken_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => TokenHelpers.GetTokenEnd(null!));
    }

    [Fact]
    public void GetTokenEnd_WithEmptyText_ReturnsStartPosition()
    {
        // Arrange
        var token = new Token("", new Position(5, 10), 0);

        // Act
        var result = TokenHelpers.GetTokenEnd(token);

        // Assert
        Assert.Equal(5, result.Line);
        Assert.Equal(10, result.Character);
    }

    [Fact]
    public void GetTokenEnd_WithCROnlyLineEnding_ReturnsCorrectPosition()
    {
        // Arrange
        var token = new Token("line1\rline2", new Position(0, 0), 11);

        // Act
        var result = TokenHelpers.GetTokenEnd(token);

        // Assert
        Assert.Equal(1, result.Line);
        Assert.Equal(5, result.Character);
    }

    #region IsLikelyKeyword Tests

    [Fact]
    public void IsLikelyKeyword_WithKeywordToken_ReturnsTrue()
    {
        // Arrange
        var token = new Token("SELECT", new Position(0, 0), 6, "Keyword");

        // Act
        var result = TokenHelpers.IsLikelyKeyword(token);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsLikelyKeyword_WithIdentifierToken_ReturnsFalse()
    {
        // Arrange
        var token = new Token("users", new Position(0, 0), 5, "Identifier");

        // Act
        var result = TokenHelpers.IsLikelyKeyword(token);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsLikelyKeyword_WithWhitespaceToken_ReturnsFalse()
    {
        // Arrange
        var token = new Token("   ", new Position(0, 0), 3, "WhiteSpace");

        // Act
        var result = TokenHelpers.IsLikelyKeyword(token);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsLikelyKeyword_WithCommentToken_ReturnsFalse()
    {
        // Arrange
        var token = new Token("-- comment", new Position(0, 0), 10, "SingleLineComment");

        // Act
        var result = TokenHelpers.IsLikelyKeyword(token);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsLikelyKeyword_WithVariableToken_ReturnsFalse()
    {
        // Arrange
        var token = new Token("@var", new Position(0, 0), 4, "Variable");

        // Act
        var result = TokenHelpers.IsLikelyKeyword(token);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsLikelyKeyword_WithLiteralToken_ReturnsFalse()
    {
        // Arrange
        var token = new Token("123", new Position(0, 0), 3, "IntegerLiteral");

        // Act
        var result = TokenHelpers.IsLikelyKeyword(token);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsLikelyKeyword_WithEmptyText_ReturnsFalse()
    {
        // Arrange
        var token = new Token("", new Position(0, 0), 0);

        // Act
        var result = TokenHelpers.IsLikelyKeyword(token);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsLikelyKeyword_WithNullToken_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => TokenHelpers.IsLikelyKeyword(null!));
    }

    [Fact]
    public void IsLikelyKeyword_WithSymbol_ReturnsFalse()
    {
        // Arrange
        var token = new Token("*", new Position(0, 0), 1);

        // Act
        var result = TokenHelpers.IsLikelyKeyword(token);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsLikelyKeyword_WithWordStartingWithDigit_ReturnsFalse()
    {
        // Arrange
        var token = new Token("123abc", new Position(0, 0), 6);

        // Act
        var result = TokenHelpers.IsLikelyKeyword(token);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsLikelyKeyword_WithUnderscore_ReturnsTrue()
    {
        // Arrange
        var token = new Token("OUTER_JOIN", new Position(0, 0), 10, "Keyword");

        // Act
        var result = TokenHelpers.IsLikelyKeyword(token);

        // Assert
        Assert.True(result);
    }

    #endregion

    #region SkipTrivia Tests

    [Fact]
    public void SkipTrivia_WithConsecutiveTrivia_SkipsAllTrivia()
    {
        // Arrange
        var tokens = new List<Token>
        {
            new Token("SELECT", new Position(0, 0), 6),
            new Token("   ", new Position(0, 6), 3),
            new Token("-- comment", new Position(0, 9), 10),
            new Token("\n", new Position(0, 19), 1),
            new Token("*", new Position(1, 0), 1)
        };

        // Act
        var result = TokenHelpers.SkipTrivia(tokens, 1);

        // Assert
        Assert.Equal(4, result);
    }

    [Fact]
    public void SkipTrivia_WithNoTrivia_ReturnsStartIndex()
    {
        // Arrange
        var tokens = new List<Token>
        {
            new Token("SELECT", new Position(0, 0), 6),
            new Token("*", new Position(0, 7), 1)
        };

        // Act
        var result = TokenHelpers.SkipTrivia(tokens, 1);

        // Assert
        Assert.Equal(1, result);
    }

    [Fact]
    public void SkipTrivia_WithAllTrivia_ReturnsTokensCount()
    {
        // Arrange
        var tokens = new List<Token>
        {
            new Token("   ", new Position(0, 0), 3),
            new Token("\n", new Position(0, 3), 1),
            new Token("-- comment", new Position(1, 0), 10)
        };

        // Act
        var result = TokenHelpers.SkipTrivia(tokens, 0);

        // Assert
        Assert.Equal(3, result);
    }

    [Fact]
    public void SkipTrivia_WithStartIndexAtEnd_ReturnsTokensCount()
    {
        // Arrange
        var tokens = new List<Token>
        {
            new Token("SELECT", new Position(0, 0), 6)
        };

        // Act
        var result = TokenHelpers.SkipTrivia(tokens, 1);

        // Assert
        Assert.Equal(1, result);
    }

    [Fact]
    public void SkipTrivia_WithNullTokens_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => TokenHelpers.SkipTrivia(null!, 0));
    }

    [Fact]
    public void SkipTrivia_WithEmptyList_ReturnsZero()
    {
        // Arrange
        var tokens = new List<Token>();

        // Act
        var result = TokenHelpers.SkipTrivia(tokens, 0);

        // Assert
        Assert.Equal(0, result);
    }

    #endregion

    #region GetTokenRange Tests

    [Fact]
    public void GetTokenRange_WithValidRange_ReturnsCorrectRange()
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
        var result = TokenHelpers.GetTokenRange(tokens, 0, 4);

        // Assert
        Assert.Equal(0, result.Start.Line);
        Assert.Equal(0, result.Start.Character);
        Assert.Equal(0, result.End.Line);
        Assert.Equal(13, result.End.Character);
    }

    [Fact]
    public void GetTokenRange_WithSameStartAndEnd_ReturnsSingleTokenRange()
    {
        // Arrange
        var tokens = new List<Token>
        {
            new Token("SELECT", new Position(0, 0), 6)
        };

        // Act
        var result = TokenHelpers.GetTokenRange(tokens, 0, 0);

        // Assert
        Assert.Equal(0, result.Start.Character);
        Assert.Equal(6, result.End.Character);
    }

    [Fact]
    public void GetTokenRange_WithMultiLineTokens_ReturnsCorrectRange()
    {
        // Arrange
        var tokens = new List<Token>
        {
            new Token("SELECT", new Position(0, 0), 6),
            new Token("\n", new Position(0, 6), 1),
            new Token("*", new Position(1, 0), 1)
        };

        // Act
        var result = TokenHelpers.GetTokenRange(tokens, 0, 2);

        // Assert
        Assert.Equal(0, result.Start.Line);
        Assert.Equal(0, result.Start.Character);
        Assert.Equal(1, result.End.Line);
        Assert.Equal(1, result.End.Character);
    }

    [Fact]
    public void GetTokenRange_WithNullTokens_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => TokenHelpers.GetTokenRange(null!, 0, 0));
    }

    [Fact]
    public void GetTokenRange_WithNegativeStartIndex_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var tokens = new List<Token> { new Token("SELECT", new Position(0, 0), 6) };

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => TokenHelpers.GetTokenRange(tokens, -1, 0));
    }

    [Fact]
    public void GetTokenRange_WithStartIndexOutOfRange_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var tokens = new List<Token> { new Token("SELECT", new Position(0, 0), 6) };

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => TokenHelpers.GetTokenRange(tokens, 5, 5));
    }

    [Fact]
    public void GetTokenRange_WithEndIndexOutOfRange_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var tokens = new List<Token> { new Token("SELECT", new Position(0, 0), 6) };

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => TokenHelpers.GetTokenRange(tokens, 0, 5));
    }

    [Fact]
    public void GetTokenRange_WithStartGreaterThanEnd_ThrowsArgumentException()
    {
        // Arrange
        var tokens = new List<Token>
        {
            new Token("SELECT", new Position(0, 0), 6),
            new Token("*", new Position(0, 7), 1)
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => TokenHelpers.GetTokenRange(tokens, 1, 0));
    }

    #endregion

    #region FindTokenByRange Tests

    [Fact]
    public void FindTokenByRange_WithMatchingRange_ReturnsToken()
    {
        // Arrange
        var tokens = new List<Token>
        {
            new Token("SELECT", new Position(0, 0), 6),
            new Token(" ", new Position(0, 6), 1),
            new Token("*", new Position(0, 7), 1)
        };
        var range = new TsqlRefine.PluginSdk.Range(new Position(0, 0), new Position(0, 6));

        // Act
        var result = TokenHelpers.FindTokenByRange(tokens, range);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("SELECT", result.Text);
    }

    [Fact]
    public void FindTokenByRange_WithNoMatchingRange_ReturnsNull()
    {
        // Arrange
        var tokens = new List<Token>
        {
            new Token("SELECT", new Position(0, 0), 6)
        };
        var range = new TsqlRefine.PluginSdk.Range(new Position(0, 0), new Position(0, 10));

        // Act
        var result = TokenHelpers.FindTokenByRange(tokens, range);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void FindTokenByRange_SkipsTrivia_ReturnsNonTriviaToken()
    {
        // Arrange
        var tokens = new List<Token>
        {
            new Token("   ", new Position(0, 0), 3),
            new Token("SELECT", new Position(0, 3), 6)
        };
        var range = new TsqlRefine.PluginSdk.Range(new Position(0, 3), new Position(0, 9));

        // Act
        var result = TokenHelpers.FindTokenByRange(tokens, range);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("SELECT", result.Text);
    }

    [Fact]
    public void FindTokenByRange_WithNullTokens_ThrowsArgumentNullException()
    {
        // Arrange
        var range = new TsqlRefine.PluginSdk.Range(new Position(0, 0), new Position(0, 6));

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => TokenHelpers.FindTokenByRange(null!, range));
    }

    [Fact]
    public void FindTokenByRange_WithNullRange_ThrowsArgumentNullException()
    {
        // Arrange
        var tokens = new List<Token> { new Token("SELECT", new Position(0, 0), 6) };

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => TokenHelpers.FindTokenByRange(tokens, null!));
    }

    #endregion

    #region FindTokenIndexByRange Tests

    [Fact]
    public void FindTokenIndexByRange_WithMatchingRange_ReturnsIndex()
    {
        // Arrange
        var tokens = new List<Token>
        {
            new Token("SELECT", new Position(0, 0), 6),
            new Token(" ", new Position(0, 6), 1),
            new Token("*", new Position(0, 7), 1)
        };
        var range = new TsqlRefine.PluginSdk.Range(new Position(0, 7), new Position(0, 8));

        // Act
        var result = TokenHelpers.FindTokenIndexByRange(tokens, range);

        // Assert
        Assert.Equal(2, result);
    }

    [Fact]
    public void FindTokenIndexByRange_WithNoMatchingRange_ReturnsMinusOne()
    {
        // Arrange
        var tokens = new List<Token>
        {
            new Token("SELECT", new Position(0, 0), 6)
        };
        var range = new TsqlRefine.PluginSdk.Range(new Position(0, 0), new Position(0, 10));

        // Act
        var result = TokenHelpers.FindTokenIndexByRange(tokens, range);

        // Assert
        Assert.Equal(-1, result);
    }

    [Fact]
    public void FindTokenIndexByRange_SkipsTrivia_ReturnsCorrectIndex()
    {
        // Arrange
        var tokens = new List<Token>
        {
            new Token("   ", new Position(0, 0), 3),
            new Token("SELECT", new Position(0, 3), 6)
        };
        var range = new TsqlRefine.PluginSdk.Range(new Position(0, 3), new Position(0, 9));

        // Act
        var result = TokenHelpers.FindTokenIndexByRange(tokens, range);

        // Assert
        Assert.Equal(1, result);
    }

    [Fact]
    public void FindTokenIndexByRange_WithNullTokens_ThrowsArgumentNullException()
    {
        // Arrange
        var range = new TsqlRefine.PluginSdk.Range(new Position(0, 0), new Position(0, 6));

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => TokenHelpers.FindTokenIndexByRange(null!, range));
    }

    [Fact]
    public void FindTokenIndexByRange_WithNullRange_ThrowsArgumentNullException()
    {
        // Arrange
        var tokens = new List<Token> { new Token("SELECT", new Position(0, 0), 6) };

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => TokenHelpers.FindTokenIndexByRange(tokens, null!));
    }

    #endregion

    #region IsPositionWithinRange Tests

    [Fact]
    public void IsPositionWithinRange_WithPositionInside_ReturnsTrue()
    {
        // Arrange
        var position = new Position(1, 5);
        var range = new TsqlRefine.PluginSdk.Range(new Position(0, 0), new Position(2, 10));

        // Act
        var result = TokenHelpers.IsPositionWithinRange(position, range);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsPositionWithinRange_WithPositionAtStart_ReturnsTrue()
    {
        // Arrange
        var position = new Position(0, 0);
        var range = new TsqlRefine.PluginSdk.Range(new Position(0, 0), new Position(0, 10));

        // Act
        var result = TokenHelpers.IsPositionWithinRange(position, range);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsPositionWithinRange_WithPositionAtEnd_ReturnsTrue()
    {
        // Arrange
        var position = new Position(0, 10);
        var range = new TsqlRefine.PluginSdk.Range(new Position(0, 0), new Position(0, 10));

        // Act
        var result = TokenHelpers.IsPositionWithinRange(position, range);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsPositionWithinRange_WithPositionBeforeStart_ReturnsFalse()
    {
        // Arrange
        var position = new Position(0, 0);
        var range = new TsqlRefine.PluginSdk.Range(new Position(0, 5), new Position(0, 10));

        // Act
        var result = TokenHelpers.IsPositionWithinRange(position, range);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsPositionWithinRange_WithPositionAfterEnd_ReturnsFalse()
    {
        // Arrange
        var position = new Position(0, 15);
        var range = new TsqlRefine.PluginSdk.Range(new Position(0, 0), new Position(0, 10));

        // Act
        var result = TokenHelpers.IsPositionWithinRange(position, range);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsPositionWithinRange_WithPositionOnLineBefore_ReturnsFalse()
    {
        // Arrange
        var position = new Position(0, 5);
        var range = new TsqlRefine.PluginSdk.Range(new Position(1, 0), new Position(1, 10));

        // Act
        var result = TokenHelpers.IsPositionWithinRange(position, range);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsPositionWithinRange_WithPositionOnLineAfter_ReturnsFalse()
    {
        // Arrange
        var position = new Position(2, 5);
        var range = new TsqlRefine.PluginSdk.Range(new Position(0, 0), new Position(1, 10));

        // Act
        var result = TokenHelpers.IsPositionWithinRange(position, range);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsPositionWithinRange_WithNullPosition_ThrowsArgumentNullException()
    {
        // Arrange
        var range = new TsqlRefine.PluginSdk.Range(new Position(0, 0), new Position(0, 10));

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => TokenHelpers.IsPositionWithinRange(null!, range));
    }

    [Fact]
    public void IsPositionWithinRange_WithNullRange_ThrowsArgumentNullException()
    {
        // Arrange
        var position = new Position(0, 5);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => TokenHelpers.IsPositionWithinRange(position, null!));
    }

    #endregion

    #region ApplyKeywordCasing Tests

    [Fact]
    public void ApplyKeywordCasing_WithUppercaseOriginal_ReturnsUppercase()
    {
        // Act
        var result = TokenHelpers.ApplyKeywordCasing("nvarchar", "VARCHAR");

        // Assert
        Assert.Equal("NVARCHAR", result);
    }

    [Fact]
    public void ApplyKeywordCasing_WithLowercaseOriginal_ReturnsLowercase()
    {
        // Act
        var result = TokenHelpers.ApplyKeywordCasing("NVARCHAR", "varchar");

        // Assert
        Assert.Equal("nvarchar", result);
    }

    [Fact]
    public void ApplyKeywordCasing_WithMixedCaseOriginal_ReturnsUppercase()
    {
        // Act
        var result = TokenHelpers.ApplyKeywordCasing("nvarchar", "VarChar");

        // Assert
        Assert.Equal("NVARCHAR", result);
    }

    [Fact]
    public void ApplyKeywordCasing_WithEmptyOriginal_ReturnsReplacement()
    {
        // Act
        var result = TokenHelpers.ApplyKeywordCasing("nvarchar", "");

        // Assert
        Assert.Equal("nvarchar", result);
    }

    [Fact]
    public void ApplyKeywordCasing_WithNullOriginal_ReturnsReplacement()
    {
        // Act
        var result = TokenHelpers.ApplyKeywordCasing("nvarchar", null);

        // Assert
        Assert.Equal("nvarchar", result);
    }

    [Fact]
    public void ApplyKeywordCasing_WithNullReplacement_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => TokenHelpers.ApplyKeywordCasing(null!, "VARCHAR"));
    }

    [Fact]
    public void ApplyKeywordCasing_WithNonLetterOriginal_ReturnsUppercase()
    {
        // Act
        var result = TokenHelpers.ApplyKeywordCasing("nvarchar", "123");

        // Assert
        Assert.Equal("NVARCHAR", result);
    }

    #endregion
}
