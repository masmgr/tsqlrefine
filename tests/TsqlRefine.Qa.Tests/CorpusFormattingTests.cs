using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.Formatting;

namespace TsqlRefine.Qa.Tests;

public sealed class CorpusFormattingTests
{
    [Fact]
    public void Format_CorpusAndMutations_IsIdempotentAndLexicallyNonDestructive()
    {
        foreach (var file in CorpusSupport.LoadFiles())
        {
            var input = CorpusSupport.Read(file);
            foreach (var candidate in new[] { input, MutateTokenBoundaries(input, file.MinCompatLevel) })
            {
                var options = new FormattingOptions
                {
                    CompatLevel = file.MinCompatLevel,
                    LineEnding = LineEnding.Lf,
                    InsertFinalNewline = true
                };
                var once = SqlFormatter.Format(candidate, options);
                var twice = SqlFormatter.Format(once, options);

                Assert.Equal(once, twice);
                AssertTokenEquivalent(candidate, once, file.MinCompatLevel);
            }
        }
    }

    private static string MutateTokenBoundaries(string sql, int compatibilityLevel)
    {
        using var reader = new StringReader(sql);
        var tokens = CorpusSupport.CreateParser(compatibilityLevel).GetTokenStream(reader, out _);
        var pieces = tokens
            .Where(token => token.TokenType != TSqlTokenType.EndOfFile)
            .Select((token, index) => token.TokenType switch
            {
                TSqlTokenType.WhiteSpace => index % 2 == 0 ? "  " : "\n",
                TSqlTokenType.Identifier or TSqlTokenType.QuotedIdentifier or
                    TSqlTokenType.AsciiStringLiteral or TSqlTokenType.UnicodeStringLiteral => token.Text,
                TSqlTokenType.SingleLineComment or TSqlTokenType.MultilineComment => token.Text,
                _ => index % 2 == 0 ? token.Text?.ToLowerInvariant() : token.Text?.ToUpperInvariant()
            });
        return "/* corpus mutation */\n" + string.Concat(pieces);
    }

    private static void AssertTokenEquivalent(string before, string after, int compatibilityLevel)
    {
        var beforeTokens = SignificantTokens(before, compatibilityLevel);
        var afterTokens = SignificantTokens(after, compatibilityLevel);
        Assert.Equal(beforeTokens.Count, afterTokens.Count);

        for (var index = 0; index < beforeTokens.Count; index++)
        {
            var expected = beforeTokens[index];
            var actual = afterTokens[index];
            Assert.Equal(expected.TokenType, actual.TokenType);
            if (IsKeyword(expected.TokenType))
            {
                Assert.True(string.Equals(expected.Text, actual.Text, StringComparison.OrdinalIgnoreCase),
                    $"Keyword token {index} changed from '{expected.Text}' to '{actual.Text}'.");
            }
            else
            {
                Assert.Equal(expected.Text, actual.Text);
            }
        }
    }

    private static IReadOnlyList<TSqlParserToken> SignificantTokens(string sql, int compatibilityLevel)
    {
        using var reader = new StringReader(sql);
        return CorpusSupport.CreateParser(compatibilityLevel).GetTokenStream(reader, out _)
            .Where(token => token.TokenType is not TSqlTokenType.WhiteSpace and not TSqlTokenType.EndOfFile)
            .ToArray();
    }

    private static bool IsKeyword(TSqlTokenType tokenType) => tokenType is not (
        TSqlTokenType.Identifier or
        TSqlTokenType.QuotedIdentifier or
        TSqlTokenType.AsciiStringLiteral or
        TSqlTokenType.UnicodeStringLiteral or
        TSqlTokenType.Integer or
        TSqlTokenType.Numeric or
        TSqlTokenType.Real or
        TSqlTokenType.Money or
        TSqlTokenType.SingleLineComment or
        TSqlTokenType.MultilineComment);
}
