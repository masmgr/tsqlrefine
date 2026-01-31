using System.IO;
using System.Runtime.CompilerServices;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Core.Engine;

internal sealed record ScriptDomAnalysis(ScriptDomAst Ast, IReadOnlyList<Token> Tokens);

internal static class ScriptDomTokenizer
{
    private static readonly IReadOnlyDictionary<TSqlTokenType, string> TokenTypeNameCache = BuildTokenTypeNameCache();

    public static ScriptDomAnalysis Analyze(string sql, int compatLevel)
    {
        var text = sql ?? string.Empty;

        try
        {
            var parser = CreateParser(compatLevel);

            using var parseReader = new StringReader(text);
            var fragment = parser.Parse(parseReader, out IList<ParseError> parseErrors);

            using var tokenReader = new StringReader(text);
            var tokenStream = parser.GetTokenStream(tokenReader, out IList<ParseError> tokenErrors);

            var tokens = MapTokens(tokenStream);
            var ast = new ScriptDomAst(text, fragment, parseErrors.ToArray(), tokenErrors.ToArray());
            return new ScriptDomAnalysis(ast, tokens);
        }
        catch (Exception ex)
        {
            var ast = new ScriptDomAst(text, null, Array.Empty<ParseError>(), Array.Empty<ParseError>(), ex);
            return new ScriptDomAnalysis(ast, Array.Empty<Token>());
        }
    }

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

    private static IReadOnlyList<Token> MapTokens(IList<TSqlParserToken> tokenStream)
    {
        if (tokenStream is null || tokenStream.Count == 0)
        {
            return Array.Empty<Token>();
        }

        var tokens = new List<Token>(tokenStream.Count);
        foreach (var token in tokenStream)
        {
            if (token.TokenType == TSqlTokenType.EndOfFile)
            {
                continue;
            }

            var text = token.Text ?? string.Empty;
            tokens.Add(new Token(
                text,
                new Position(Math.Max(0, token.Line - 1), Math.Max(0, token.Column - 1)),
                text.Length,
                GetTokenTypeName(token.TokenType)
            ));
        }

        return tokens;
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
}
