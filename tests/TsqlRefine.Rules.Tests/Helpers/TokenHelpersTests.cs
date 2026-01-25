using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers;
using Xunit;

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
}
