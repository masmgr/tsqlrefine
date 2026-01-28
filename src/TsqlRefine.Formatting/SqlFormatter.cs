using System.Text;
using System.Runtime.CompilerServices;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace TsqlRefine.Formatting;

public static class SqlFormatter
{
    public static string Format(string sql, FormattingOptions? options = null)
    {
        if (string.IsNullOrEmpty(sql))
        {
            return string.Empty;
        }

        options ??= new FormattingOptions();
        var keywordCased = ScriptDomKeywordCaser.Apply(sql, options.KeywordCasing, options.IdentifierCasing);
        var whitespaceNormalized = new MinimalWhitespaceNormalizer(options).Format(keywordCased);

        // Apply comma style if not default trailing
        if (options.CommaStyle == CommaStyle.Leading)
        {
            whitespaceNormalized = ApplyLeadingCommaStyle(whitespaceNormalized);
        }

        return whitespaceNormalized;
    }

    private static string ApplyLeadingCommaStyle(string input)
    {
        // Simple transformation: move trailing commas to leading position
        // This is a basic implementation that handles simple cases
        // For more complex scenarios, a full AST-based approach would be needed
        var lines = input.Split('\n');
        var result = new StringBuilder(input.Length);

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var trimmed = line.TrimEnd();

            if (trimmed.EndsWith(','))
            {
                // This line has a trailing comma
                var withoutComma = trimmed[..^1].TrimEnd();
                result.Append(withoutComma);

                // If there's a next line, prepend the comma to it
                if (i + 1 < lines.Length)
                {
                    result.Append('\n');
                    var nextLine = lines[i + 1];
                    var nextTrimStart = nextLine.TrimStart();
                    var leadingWhitespace = nextLine[..^nextTrimStart.Length];
                    result.Append(leadingWhitespace);
                    result.Append(',');
                    if (nextTrimStart.Length > 0)
                    {
                        result.Append(' ');
                        result.Append(nextTrimStart);
                    }
                    i++; // Skip next line as we already processed it
                }
                else
                {
                    // Last line with comma, keep it trailing
                    result.Append(',');
                }
            }
            else
            {
                result.Append(line);
            }

            if (i < lines.Length - 1)
            {
                result.Append('\n');
            }
        }

        return result.ToString();
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

        public static string Apply(string input, KeywordCasing keywordCasing, IdentifierCasing identifierCasing)
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

                if (IsKeywordToken(token))
                {
                    sb.Append(ApplyCasing(text, keywordCasing));
                }
                else if (IsIdentifierToken(token))
                {
                    sb.Append(ApplyCasing(text, identifierCasing));
                }
                else
                {
                    sb.Append(text);
                }
            }

            return sb.ToString();
        }

        private static string ApplyCasing(string text, KeywordCasing casing) => casing switch
        {
            KeywordCasing.Upper => text.ToUpperInvariant(),
            KeywordCasing.Lower => text.ToLowerInvariant(),
            KeywordCasing.Pascal => ToPascalCase(text),
            KeywordCasing.Preserve => text,
            _ => text
        };

        private static string ApplyCasing(string text, IdentifierCasing casing) => casing switch
        {
            IdentifierCasing.Upper => text.ToUpperInvariant(),
            IdentifierCasing.Lower => text.ToLowerInvariant(),
            IdentifierCasing.Pascal => ToPascalCase(text),
            IdentifierCasing.Camel => ToCamelCase(text),
            IdentifierCasing.Preserve => text,
            _ => text
        };

        private static string ToPascalCase(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            var lower = text.ToLowerInvariant();
            return char.ToUpperInvariant(lower[0]) + lower[1..];
        }

        private static string ToCamelCase(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            var lower = text.ToLowerInvariant();
            return char.ToLowerInvariant(lower[0]) + lower[1..];
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

            var result = sb.ToString();

            // Apply final newline option
            if (_options.InsertFinalNewline && !result.EndsWith('\n'))
            {
                result += '\n';
            }

            return result;
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

            if (_options.TrimTrailingWhitespace && !lineContainsProtected && !IsInProtectedRegion())
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
