using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.Formatting.Helpers;
using Xunit;

namespace TsqlRefine.Formatting.Tests.Helpers;

public class ScriptDomTokenHelperTests
{
    [Fact]
    public void BuildNonTriviaNeighborIndexes_MatchesIndividualBuilders()
    {
        var tokens = ParseTokens("SELECT id, -- c1\r\nname FROM dbo.Users");

        var expectedPrevious = ScriptDomTokenHelper.BuildPreviousNonTriviaIndexes(tokens);
        var expectedNext = ScriptDomTokenHelper.BuildNextNonTriviaIndexes(tokens);

        var (actualPrevious, actualNext) = ScriptDomTokenHelper.BuildNonTriviaNeighborIndexes(tokens);

        Assert.Equal(expectedPrevious, actualPrevious);
        Assert.Equal(expectedNext, actualNext);
    }

    [Fact]
    public void BuildNonTriviaNeighborIndexes_SkipsTriviaTokens()
    {
        var tokens = ParseTokens("SELECT  id\r\nFROM dbo.Users");
        var (previous, next) = ScriptDomTokenHelper.BuildNonTriviaNeighborIndexes(tokens);

        var selectIndex = FindTokenIndex(tokens, "SELECT");
        var idIndex = FindTokenIndex(tokens, "id");
        var usersIndex = FindTokenIndex(tokens, "Users");

        Assert.Equal(selectIndex, previous[idIndex]);
        Assert.Equal(idIndex, next[selectIndex]);

        var whitespaceIndex = FindFirstTriviaIndex(tokens, TSqlTokenType.WhiteSpace);
        Assert.Equal(selectIndex, previous[whitespaceIndex]);
        Assert.Equal(idIndex, next[whitespaceIndex]);

        var eofIndex = FindTokenIndexByType(tokens, TSqlTokenType.EndOfFile);
        Assert.Equal(usersIndex, previous[eofIndex]);
        Assert.Equal(-1, next[eofIndex]);
    }

    // CreateParser tests

    [Theory]
    [InlineData(90, typeof(TSql100Parser))]
    [InlineData(100, typeof(TSql100Parser))]
    [InlineData(110, typeof(TSql110Parser))]
    [InlineData(120, typeof(TSql120Parser))]
    [InlineData(130, typeof(TSql130Parser))]
    [InlineData(140, typeof(TSql140Parser))]
    [InlineData(150, typeof(TSql150Parser))]
    [InlineData(160, typeof(TSql160Parser))]
    [InlineData(170, typeof(TSql160Parser))]
    public void CreateParser_CompatLevel_ReturnsCorrectParserType(int compatLevel, Type expectedType)
    {
        var parser = ScriptDomTokenHelper.CreateParser(compatLevel);

        Assert.IsType(expectedType, parser);
    }

    // IsTrivia tests

    [Theory]
    [InlineData("SELECT  id", TSqlTokenType.WhiteSpace, true)]
    [InlineData("SELECT -- comment\nid", TSqlTokenType.SingleLineComment, true)]
    [InlineData("SELECT /* block */ id", TSqlTokenType.MultilineComment, true)]
    public void IsTrivia_TriviaTokenTypes_ReturnsTrue(string sql, TSqlTokenType targetType, bool expected)
    {
        var tokens = ParseTokens(sql);
        var token = tokens.First(t => t.TokenType == targetType);

        Assert.Equal(expected, ScriptDomTokenHelper.IsTrivia(token));
    }

    [Fact]
    public void IsTrivia_KeywordToken_ReturnsFalse()
    {
        var tokens = ParseTokens("SELECT id");
        var selectToken = tokens.First(t => t.TokenType == TSqlTokenType.Select);

        Assert.False(ScriptDomTokenHelper.IsTrivia(selectToken));
    }

    // TriviaTokenTypes tests

    [Fact]
    public void TriviaTokenTypes_ContainsWhitespaceAndCommentTypes()
    {
        var types = (IReadOnlySet<TSqlTokenType>)ScriptDomTokenHelper.TriviaTokenTypes;
        Assert.Contains(TSqlTokenType.WhiteSpace, types);
        Assert.Contains(TSqlTokenType.SingleLineComment, types);
        Assert.Contains(TSqlTokenType.MultilineComment, types);
    }

    [Fact]
    public void TriviaTokenTypes_DoesNotContainNonTriviaTypes()
    {
        var types = (IReadOnlySet<TSqlTokenType>)ScriptDomTokenHelper.TriviaTokenTypes;
        Assert.DoesNotContain(TSqlTokenType.Select, types);
        Assert.DoesNotContain(TSqlTokenType.From, types);
        Assert.DoesNotContain(TSqlTokenType.Identifier, types);
        Assert.DoesNotContain(TSqlTokenType.Integer, types);
    }

    // BuildPreviousNonTriviaIndexes tests

    [Fact]
    public void BuildPreviousNonTriviaIndexes_EmptyTokenList_ReturnsEmptyArray()
    {
        var result = ScriptDomTokenHelper.BuildPreviousNonTriviaIndexes(Array.Empty<TSqlParserToken>());

        Assert.Empty(result);
    }

    [Fact]
    public void BuildPreviousNonTriviaIndexes_AllTrivia_AllNegativeOne()
    {
        // Parse whitespace-only input; the token stream will contain whitespace + EOF
        var tokens = ParseTokens("   ");
        var result = ScriptDomTokenHelper.BuildPreviousNonTriviaIndexes(tokens);

        // All trivia tokens before the first non-trivia should be -1
        for (var i = 0; i < result.Length; i++)
        {
            if (ScriptDomTokenHelper.IsTrivia(tokens[i]))
            {
                Assert.Equal(-1, result[i]);
            }
        }
    }

    [Fact]
    public void BuildPreviousNonTriviaIndexes_SingleNonTrivia_FirstIsNegativeOne()
    {
        var tokens = ParseTokens("SELECT");
        var result = ScriptDomTokenHelper.BuildPreviousNonTriviaIndexes(tokens);

        // First token (SELECT) should have no previous non-trivia
        Assert.Equal(-1, result[0]);
    }

    [Fact]
    public void BuildPreviousNonTriviaIndexes_MultipleNonTrivia_PointsToPrevious()
    {
        var tokens = ParseTokens("SELECT id FROM t");
        var result = ScriptDomTokenHelper.BuildPreviousNonTriviaIndexes(tokens);

        var selectIdx = FindTokenIndex(tokens, "SELECT");
        var idIdx = FindTokenIndex(tokens, "id");
        var fromIdx = FindTokenIndex(tokens, "FROM");

        Assert.Equal(-1, result[selectIdx]);
        Assert.Equal(selectIdx, result[idIdx]);
        Assert.Equal(idIdx, result[fromIdx]);
    }

    // BuildNextNonTriviaIndexes tests

    [Fact]
    public void BuildNextNonTriviaIndexes_EmptyTokenList_ReturnsEmptyArray()
    {
        var result = ScriptDomTokenHelper.BuildNextNonTriviaIndexes(Array.Empty<TSqlParserToken>());

        Assert.Empty(result);
    }

    [Fact]
    public void BuildNextNonTriviaIndexes_AllTrivia_TriviaPointsToEof()
    {
        // Whitespace-only input produces trivia tokens + EOF (non-trivia)
        var tokens = ParseTokens("   ");
        var result = ScriptDomTokenHelper.BuildNextNonTriviaIndexes(tokens);
        var eofIdx = FindTokenIndexByType(tokens, TSqlTokenType.EndOfFile);

        for (var i = 0; i < result.Length; i++)
        {
            if (ScriptDomTokenHelper.IsTrivia(tokens[i]))
            {
                // Trivia tokens should point to EOF as next non-trivia
                Assert.Equal(eofIdx, result[i]);
            }
        }

        // EOF itself should have no next
        Assert.Equal(-1, result[eofIdx]);
    }

    [Fact]
    public void BuildNextNonTriviaIndexes_SingleNonTrivia_LastIsNegativeOne()
    {
        var tokens = ParseTokens("SELECT");
        var result = ScriptDomTokenHelper.BuildNextNonTriviaIndexes(tokens);

        var selectIdx = FindTokenIndex(tokens, "SELECT");
        // SELECT's next non-trivia should be EOF
        var eofIdx = FindTokenIndexByType(tokens, TSqlTokenType.EndOfFile);
        Assert.Equal(eofIdx, result[selectIdx]);
        // EOF should have no next
        Assert.Equal(-1, result[eofIdx]);
    }

    [Fact]
    public void BuildNextNonTriviaIndexes_MultipleNonTrivia_PointsToNext()
    {
        var tokens = ParseTokens("SELECT id FROM t");
        var result = ScriptDomTokenHelper.BuildNextNonTriviaIndexes(tokens);

        var selectIdx = FindTokenIndex(tokens, "SELECT");
        var idIdx = FindTokenIndex(tokens, "id");
        var fromIdx = FindTokenIndex(tokens, "FROM");

        Assert.Equal(idIdx, result[selectIdx]);
        Assert.Equal(fromIdx, result[idIdx]);
    }

    private static IList<TSqlParserToken> ParseTokens(string sql)
    {
        var parser = ScriptDomTokenHelper.CreateParser(150);
        using var reader = new StringReader(sql);
        return parser.GetTokenStream(reader, out _);
    }

    private static int FindTokenIndex(IList<TSqlParserToken> tokens, string text)
    {
        for (var i = 0; i < tokens.Count; i++)
        {
            if (string.Equals(tokens[i].Text, text, StringComparison.Ordinal))
            {
                return i;
            }
        }

        throw new InvalidOperationException($"Token not found: {text}");
    }

    private static int FindTokenIndexByType(IList<TSqlParserToken> tokens, TSqlTokenType tokenType)
    {
        for (var i = 0; i < tokens.Count; i++)
        {
            if (tokens[i].TokenType == tokenType)
            {
                return i;
            }
        }

        throw new InvalidOperationException($"Token not found: {tokenType}");
    }

    private static int FindFirstTriviaIndex(IList<TSqlParserToken> tokens, TSqlTokenType tokenType)
    {
        for (var i = 0; i < tokens.Count; i++)
        {
            if (tokens[i].TokenType == tokenType && ScriptDomTokenHelper.IsTrivia(tokens[i]))
            {
                return i;
            }
        }

        throw new InvalidOperationException($"Trivia token not found: {tokenType}");
    }
}
