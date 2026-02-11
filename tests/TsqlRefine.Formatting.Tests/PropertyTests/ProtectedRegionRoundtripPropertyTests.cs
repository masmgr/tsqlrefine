using FsCheck.Xunit;

namespace TsqlRefine.Formatting.Tests.PropertyTests;

/// <summary>
/// Property-based tests verifying formatting preserves content inside
/// protected regions (string literals, comments, bracket identifiers).
/// </summary>
public sealed class ProtectedRegionRoundtripPropertyTests
{
    /// <summary>
    /// Safe characters for generating string literal content.
    /// Excludes single quotes to avoid breaking the literal boundary.
    /// </summary>
    private static readonly char[] SafeStringChars =
        "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 ,.:;+-*/=<>!@#$%^&()[]{}|\\~`".ToCharArray();

    /// <summary>
    /// Safe characters for block comment content.
    /// Excludes '*' followed by '/' to avoid prematurely closing the comment.
    /// </summary>
    private static readonly char[] SafeCommentChars =
        "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 ,.:;+-=<>!@#$%^&()[]{}|\\~`'\"".ToCharArray();

    /// <summary>
    /// Safe characters for bracket identifier content.
    /// Excludes ']' to avoid breaking the identifier boundary.
    /// </summary>
    private static readonly char[] SafeBracketChars =
        "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 ,.:;+-*/=<>!@#$%^&(){}|\\~`'\"".ToCharArray();

    /// <summary>
    /// Safe characters for line comment content.
    /// Excludes newline characters.
    /// </summary>
    private static readonly char[] SafeLineCommentChars =
        "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 ,.:;+-*/=<>!@#$%^&()[]{}|\\~`'\"".ToCharArray();

    private static string BuildContent(int[] charIndexes, char[] charPool)
    {
        if (charIndexes.Length == 0)
        {
            return "x";
        }

        var chars = new char[charIndexes.Length];
        for (var i = 0; i < charIndexes.Length; i++)
        {
            chars[i] = charPool[((charIndexes[i] % charPool.Length) + charPool.Length) % charPool.Length];
        }

        return new string(chars);
    }

    [Property(MaxTest = 200)]
    public bool Format_PreservesStringLiteralContent(int idx0, int idx1, int idx2, int idx3, int idx4)
    {
        var content = BuildContent([idx0, idx1, idx2, idx3, idx4], SafeStringChars);
        var sql = $"SELECT '{content}' FROM t";
        var result = SqlFormatter.Format(sql, new FormattingOptions { InsertFinalNewline = false });

        return result.Contains($"'{content}'", StringComparison.Ordinal);
    }

    [Property(MaxTest = 200)]
    public bool Format_PreservesBlockCommentContent(int idx0, int idx1, int idx2, int idx3, int idx4)
    {
        var content = BuildContent([idx0, idx1, idx2, idx3, idx4], SafeCommentChars);
        var sql = $"SELECT /* {content} */ 1";
        var result = SqlFormatter.Format(sql, new FormattingOptions { InsertFinalNewline = false });

        return result.Contains($"/* {content} */", StringComparison.Ordinal);
    }

    [Property(MaxTest = 200)]
    public bool Format_PreservesBracketIdentifierContent(int idx0, int idx1, int idx2, int idx3, int idx4)
    {
        var content = BuildContent([idx0, idx1, idx2, idx3, idx4], SafeBracketChars);
        var sql = $"SELECT [{content}] FROM t";
        var result = SqlFormatter.Format(sql, new FormattingOptions { InsertFinalNewline = false });

        return result.Contains($"[{content}]", StringComparison.Ordinal);
    }

    [Property(MaxTest = 200)]
    public bool Format_PreservesLineCommentContent(int idx0, int idx1, int idx2, int idx3, int idx4)
    {
        var content = BuildContent([idx0, idx1, idx2, idx3, idx4], SafeLineCommentChars);
        var sql = $"SELECT 1 -- {content}";
        var result = SqlFormatter.Format(sql, new FormattingOptions { InsertFinalNewline = false });

        return result.Contains($"-- {content}", StringComparison.Ordinal);
    }

    [Property(MaxTest = 200)]
    public bool Format_ProtectedRegionsPreserved_WithVariousOptions(
        int idx0, int idx1, int idx2, int idx3, int idx4,
        bool useSpaces, int sizeIdx, int casingIdx, bool leading, bool useLf,
        bool normInline, bool normOp, bool normKeyword)
    {
        var stringContent = BuildContent([idx0, idx1, idx2], SafeStringChars);
        var commentContent = BuildContent([idx3, idx4], SafeCommentChars);
        var sql = $"SELECT '{stringContent}', /* {commentContent} */ 1 FROM t";

        int[] sizes = [2, 4, 8];
        ElementCasing[] casings = [ElementCasing.Upper, ElementCasing.Lower, ElementCasing.None];

        var opts = new FormattingOptions
        {
            IndentStyle = useSpaces ? IndentStyle.Spaces : IndentStyle.Tabs,
            IndentSize = sizes[((sizeIdx % sizes.Length) + sizes.Length) % sizes.Length],
            KeywordElementCasing = casings[((casingIdx % casings.Length) + casings.Length) % casings.Length],
            CommaStyle = leading ? CommaStyle.Leading : CommaStyle.Trailing,
            LineEnding = useLf ? LineEnding.Lf : LineEnding.CrLf,
            InsertFinalNewline = false,
            TrimTrailingWhitespace = true,
            NormalizeInlineSpacing = normInline,
            NormalizeOperatorSpacing = normOp,
            NormalizeKeywordSpacing = normKeyword,
        };

        var result = SqlFormatter.Format(sql, opts);

        return result.Contains($"'{stringContent}'", StringComparison.Ordinal) &&
               result.Contains($"/* {commentContent} */", StringComparison.Ordinal);
    }
}
