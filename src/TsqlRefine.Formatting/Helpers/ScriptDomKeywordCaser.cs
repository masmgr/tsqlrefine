using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace TsqlRefine.Formatting.Helpers;

/// <summary>
/// Applies keyword and identifier casing transformations using ScriptDom token stream.
/// </summary>
public static class ScriptDomKeywordCaser
{
    private static readonly IReadOnlyDictionary<TSqlTokenType, string> TokenTypeNameCache = BuildTokenTypeNameCache();

    private static readonly string[] NonKeywordTokenKindHints =
    {
        "Identifier",
        "Comment",
        "WhiteSpace",
        "Whitespace",
        "Variable",
        "Literal"
    };

    /// <summary>
    /// Applies keyword and identifier casing to SQL text.
    /// </summary>
    /// <param name="input">The SQL text to transform</param>
    /// <param name="keywordCasing">Casing style for SQL keywords</param>
    /// <param name="identifierCasing">Casing style for identifiers</param>
    /// <param name="compatLevel">SQL Server compatibility level (100-160). Defaults to 150 (SQL Server 2019)</param>
    /// <returns>SQL text with casing applied</returns>
    public static string Apply(
        string input,
        KeywordCasing keywordCasing,
        IdentifierCasing identifierCasing,
        int compatLevel = 150)
    {
        var parser = CreateParser(compatLevel);
        using var reader = new StringReader(input);
        var tokens = parser.GetTokenStream(reader, out _);

        var sb = new StringBuilder(input.Length + 16);
        foreach (var token in tokens)
        {
            if (token.TokenType == TSqlTokenType.EndOfFile)
            {
                continue;
            }

            var text = token.Text ?? string.Empty;

            if (IsKeywordToken(token))
            {
                sb.Append(CasingHelpers.ApplyCasing(text, keywordCasing));
            }
            else if (IsIdentifierToken(token))
            {
                sb.Append(CasingHelpers.ApplyCasing(text, identifierCasing));
            }
            else
            {
                sb.Append(text);
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Creates a T-SQL parser for the specified compatibility level.
    /// </summary>
    /// <param name="compatLevel">SQL Server compatibility level (100-160)</param>
    /// <returns>TSqlParser instance for the specified version</returns>
    private static TSqlParser CreateParser(int compatLevel) =>
        compatLevel switch
        {
            >= 160 => new TSql160Parser(initialQuotedIdentifiers: true),
            >= 150 => new TSql150Parser(initialQuotedIdentifiers: true),
            >= 140 => new TSql140Parser(initialQuotedIdentifiers: true),
            >= 130 => new TSql130Parser(initialQuotedIdentifiers: true),
            >= 120 => new TSql120Parser(initialQuotedIdentifiers: true),
            >= 110 => new TSql110Parser(initialQuotedIdentifiers: true),
            _ => new TSql100Parser(initialQuotedIdentifiers: true)
        };

    private static bool IsKeywordToken(TSqlParserToken token)
    {
        if (string.IsNullOrEmpty(token.Text))
        {
            return false;
        }

        if (!IsWordToken(token.Text))
        {
            return false;
        }

        if (IsNonKeywordTokenKind(token))
        {
            return false;
        }

        return true;
    }

    private static bool IsIdentifierToken(TSqlParserToken token)
    {
        if (string.IsNullOrEmpty(token.Text))
        {
            return false;
        }

        var typeName = GetTokenTypeName(token.TokenType);
        return typeName.Contains("Identifier", StringComparison.Ordinal) &&
               !typeName.Contains("Quoted", StringComparison.Ordinal) &&
               !token.Text.StartsWith('[') &&
               !token.Text.StartsWith('"');
    }

    private static bool IsNonKeywordTokenKind(TSqlParserToken token)
    {
        var typeName = GetTokenTypeName(token.TokenType);
        foreach (var kind in NonKeywordTokenKindHints)
        {
            if (typeName.Contains(kind, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string GetTokenTypeName(TSqlTokenType tokenType) =>
        TokenTypeNameCache.TryGetValue(tokenType, out var name) ? name : tokenType.ToString();

    private static IReadOnlyDictionary<TSqlTokenType, string> BuildTokenTypeNameCache()
    {
        var values = Enum.GetValues<TSqlTokenType>();
        var map = new Dictionary<TSqlTokenType, string>(values.Length);
        foreach (var value in values)
        {
            map[value] = value.ToString();
        }

        return map;
    }

    private static bool IsWordToken(string text)
    {
        if (string.IsNullOrEmpty(text) || !char.IsLetter(text[0]))
        {
            return false;
        }

        for (var i = 1; i < text.Length; i++)
        {
            var c = text[i];
            if (!char.IsLetterOrDigit(c) && c != '_')
            {
                return false;
            }
        }

        return true;
    }
}
