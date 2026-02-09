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
