using System.Text;
using System.Runtime.CompilerServices;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace TsqlRefine.Formatting;

public static class SqlFormatter
{
    public static string Format(string sql, FormattingOptions options)
    {
        if (string.IsNullOrEmpty(sql))
        {
            return string.Empty;
        }

        options ??= new FormattingOptions();
        var keywordCased = ScriptDomKeywordCaser.Apply(sql);
        return new MinimalWhitespaceNormalizer(options).Format(keywordCased);
    }

    private static class ScriptDomKeywordCaser
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

        public static string Apply(string input)
        {
            var parser = new TSql150Parser(initialQuotedIdentifiers: true);
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
                sb.Append(IsKeywordToken(token) ? text.ToUpperInvariant() : text);
            }

            return sb.ToString();
        }

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

    private sealed class MinimalWhitespaceNormalizer
    {
        private readonly FormattingOptions _options;
        private bool _inString;
        private bool _inDoubleQuote;
        private bool _inBracket;
        private bool _inBlockComment;

        public MinimalWhitespaceNormalizer(FormattingOptions options)
        {
            _options = options;
        }

        public string Format(string input)
        {
            var sb = new StringBuilder(input.Length + 16);
            var line = new StringBuilder();

            for (var i = 0; i < input.Length; i++)
            {
                var c = input[i];

                if (TryConsumeNewline(input, ref i, c))
                {
                    AppendProcessedLine(sb, line);
                    sb.Append('\n');
                    line.Clear();
                    continue;
                }

                line.Append(c);
            }

            if (line.Length > 0)
            {
                AppendProcessedLine(sb, line);
            }

            return sb.ToString();
        }

        private static bool TryConsumeNewline(string input, ref int index, char current)
        {
            if (current is not ('\r' or '\n'))
            {
                return false;
            }

            if (current == '\r' && index + 1 < input.Length && input[index + 1] == '\n')
            {
                index++;
            }

            return true;
        }

        private void AppendProcessedLine(StringBuilder output, StringBuilder line)
        {
            if (line.Length == 0)
            {
                return;
            }

            var text = line.ToString();
            var lineStartsInProtected = IsInProtectedRegion();
            var lineContainsProtected = lineStartsInProtected;

            var indentSize = GetIndentSize();
            GetLeadingWhitespace(text, indentSize, out var leadingLength, out var columns);

            var sbLine = new StringBuilder(text.Length);
            if (lineStartsInProtected)
            {
                sbLine.Append(text.AsSpan(0, leadingLength));
            }
            else
            {
                sbLine.Append(BuildIndent(columns, indentSize));
            }

            var i = leadingLength;
            var inLineComment = false;
            while (i < text.Length)
            {
                if (inLineComment)
                {
                    sbLine.Append(text.AsSpan(i));
                    break;
                }

                var c = text[i];

                if (TryConsumeString(text, sbLine, ref i) ||
                    TryConsumeDoubleQuote(text, sbLine, ref i) ||
                    TryConsumeBracket(text, sbLine, ref i) ||
                    TryConsumeBlockComment(text, sbLine, ref i))
                {
                    continue;
                }

                if (TryStartLineComment(text, sbLine, ref i, ref inLineComment))
                {
                    lineContainsProtected = true;
                    continue;
                }

                if (TryStartBlockComment(text, sbLine, ref i))
                {
                    lineContainsProtected = true;
                    continue;
                }

                if (TryStartString(text, sbLine, ref i) ||
                    TryStartDoubleQuote(text, sbLine, ref i) ||
                    TryStartBracket(text, sbLine, ref i))
                {
                    lineContainsProtected = true;
                    continue;
                }

                sbLine.Append(c);
                i++;
            }

            if (!lineContainsProtected && !IsInProtectedRegion())
            {
                TrimTrailingWhitespace(sbLine);
            }

            output.Append(sbLine);
        }

        private static void GetLeadingWhitespace(string text, int indentSize, out int leadingLength, out int columns)
        {
            leadingLength = 0;
            columns = 0;

            while (leadingLength < text.Length)
            {
                var c = text[leadingLength];
                if (c == ' ')
                {
                    columns++;
                    leadingLength++;
                    continue;
                }

                if (c == '\t')
                {
                    columns += indentSize;
                    leadingLength++;
                    continue;
                }

                break;
            }
        }

        private bool IsInProtectedRegion() =>
            _inString || _inBlockComment || _inDoubleQuote || _inBracket;

        private bool TryConsumeString(string text, StringBuilder output, ref int index)
        {
            if (!_inString)
            {
                return false;
            }

            var c = text[index];
            if (c == '\'')
            {
                if (index + 1 < text.Length && text[index + 1] == '\'')
                {
                    output.Append("''");
                    index += 2;
                    return true;
                }

                output.Append(c);
                _inString = false;
                index++;
                return true;
            }

            output.Append(c);
            index++;
            return true;
        }

        private bool TryConsumeDoubleQuote(string text, StringBuilder output, ref int index)
        {
            if (!_inDoubleQuote)
            {
                return false;
            }

            var c = text[index];
            output.Append(c);
            index++;
            if (c == '"')
            {
                _inDoubleQuote = false;
            }

            return true;
        }

        private bool TryConsumeBracket(string text, StringBuilder output, ref int index)
        {
            if (!_inBracket)
            {
                return false;
            }

            var c = text[index];
            if (c == ']')
            {
                if (index + 1 < text.Length && text[index + 1] == ']')
                {
                    output.Append("]]");
                    index += 2;
                    return true;
                }

                output.Append(c);
                _inBracket = false;
                index++;
                return true;
            }

            output.Append(c);
            index++;
            return true;
        }

        private bool TryConsumeBlockComment(string text, StringBuilder output, ref int index)
        {
            if (!_inBlockComment)
            {
                return false;
            }

            var c = text[index];
            if (c == '*' && index + 1 < text.Length && text[index + 1] == '/')
            {
                output.Append("*/");
                _inBlockComment = false;
                index += 2;
                return true;
            }

            output.Append(c);
            index++;
            return true;
        }

        private static bool TryStartLineComment(string text, StringBuilder output, ref int index, ref bool inLineComment)
        {
            var c = text[index];
            if (c == '-' && index + 1 < text.Length && text[index + 1] == '-')
            {
                inLineComment = true;
                output.Append(text.AsSpan(index));
                index = text.Length;
                return true;
            }

            return false;
        }

        private bool TryStartBlockComment(string text, StringBuilder output, ref int index)
        {
            var c = text[index];
            if (c == '/' && index + 1 < text.Length && text[index + 1] == '*')
            {
                _inBlockComment = true;
                output.Append("/*");
                index += 2;
                return true;
            }

            return false;
        }

        private bool TryStartString(string text, StringBuilder output, ref int index)
        {
            var c = text[index];
            if (c != '\'')
            {
                return false;
            }

            _inString = true;
            output.Append(c);
            index++;
            return true;
        }

        private bool TryStartDoubleQuote(string text, StringBuilder output, ref int index)
        {
            var c = text[index];
            if (c != '"')
            {
                return false;
            }

            _inDoubleQuote = true;
            output.Append(c);
            index++;
            return true;
        }

        private bool TryStartBracket(string text, StringBuilder output, ref int index)
        {
            var c = text[index];
            if (c != '[')
            {
                return false;
            }

            _inBracket = true;
            output.Append(c);
            index++;
            return true;
        }

        private int GetIndentSize() => _options.IndentSize <= 0 ? 4 : _options.IndentSize;

        private string BuildIndent(int columns, int indentSize)
        {
            if (columns <= 0)
            {
                return string.Empty;
            }

            if (_options.IndentStyle == IndentStyle.Tabs)
            {
                var tabs = columns / indentSize;
                var spaces = columns % indentSize;
                return new string('\t', tabs) + new string(' ', spaces);
            }

            return new string(' ', columns);
        }

        private static void TrimTrailingWhitespace(StringBuilder sb)
        {
            var i = sb.Length - 1;
            while (i >= 0 && (sb[i] == ' ' || sb[i] == '\t'))
            {
                i--;
            }

            if (i < sb.Length - 1)
            {
                sb.Length = i + 1;
            }
        }
    }
}
