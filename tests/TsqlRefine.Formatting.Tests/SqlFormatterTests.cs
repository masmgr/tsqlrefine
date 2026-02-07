using Xunit;

namespace TsqlRefine.Formatting.Tests;

public class SqlFormatterTests
{
    [Fact]
    public void Format_EmptyString_WithDefaultOptions_ReturnsCRLF()
    {
        // Default InsertFinalNewline=true with Auto mode falls back to CRLF
        var result = SqlFormatter.Format("");
        Assert.Equal("\r\n", result);
    }

    [Fact]
    public void Format_EmptyString_WithNoFinalNewline_ReturnsEmpty()
    {
        var options = new FormattingOptions { InsertFinalNewline = false };
        var result = SqlFormatter.Format("", options);
        Assert.Equal("", result);
    }

    [Fact]
    public void Format_NullString_WithDefaultOptions_ReturnsCRLF()
    {
        // Default InsertFinalNewline=true with Auto mode falls back to CRLF
        var result = SqlFormatter.Format(null!);
        Assert.Equal("\r\n", result);
    }

    [Fact]
    public void Format_NullString_WithNoFinalNewline_ReturnsEmpty()
    {
        var options = new FormattingOptions { InsertFinalNewline = false };
        var result = SqlFormatter.Format(null!, options);
        Assert.Equal("", result);
    }

    [Fact]
    public void Format_EmptyString_WithExplicitLf_ReturnsLf()
    {
        var options = new FormattingOptions { LineEnding = LineEnding.Lf };
        var result = SqlFormatter.Format("", options);
        Assert.Equal("\n", result);
    }

    [Fact]
    public void Format_EmptyString_WithExplicitCrLf_ReturnsCrLf()
    {
        var options = new FormattingOptions { LineEnding = LineEnding.CrLf };
        var result = SqlFormatter.Format("", options);
        Assert.Equal("\r\n", result);
    }

    [Fact]
    public void Format_DefaultOptions_UppercasesKeywords()
    {
        var sql = "select id, name from users";
        var result = SqlFormatter.Format(sql);

        Assert.Contains("SELECT", result);
        Assert.Contains("FROM", result);
    }

    [Fact]
    public void Format_CustomOptions_AppliesOptions()
    {
        var sql = "SELECT ID FROM USERS";
        var options = new FormattingOptions
        {
            KeywordElementCasing = ElementCasing.Lower,
            TableCasing = ElementCasing.Lower,
            ColumnCasing = ElementCasing.Lower
        };

        var result = SqlFormatter.Format(sql, options);

        Assert.Contains("select", result);
        Assert.Contains("id", result);
        Assert.Contains("users", result);
    }

    [Fact]
    public void Format_PreservesStringsAndComments()
    {
        var sql = @"SELECT 'Some String' AS text, -- comment
                   id FROM users";

        var result = SqlFormatter.Format(sql);

        Assert.Contains("'Some String'", result);
        Assert.Contains("-- comment", result);
    }

    [Fact]
    public void Format_ComplexQuery_FormatsCorrectly()
    {
        var sql = @"
select u.id,
u.name,
o.total
from users u
inner join orders o on u.id = o.user_id
where u.active = 1
order by o.total desc";

        var result = SqlFormatter.Format(sql);

        Assert.Contains("SELECT", result);
        Assert.Contains("FROM", result);
        Assert.Contains("INNER JOIN", result);
        Assert.Contains("WHERE", result);
        Assert.Contains("ORDER BY", result);
    }

    [Fact]
    public void Format_WithInsertFinalNewline_AddsNewline()
    {
        var sql = "SELECT id FROM users";
        var options = new FormattingOptions { InsertFinalNewline = true };

        var result = SqlFormatter.Format(sql, options);

        Assert.EndsWith("\n", result);
    }

    [Fact]
    public void Format_WithoutInsertFinalNewline_NoNewline()
    {
        var sql = "SELECT id FROM users";
        var options = new FormattingOptions { InsertFinalNewline = false };

        var result = SqlFormatter.Format(sql, options);

        Assert.False(result.EndsWith("\n"));
    }

    // Tests for preservation of specific elements
    // これらの要素が保持されることを確認するテスト群

    [Fact]
    public void Format_PreservesLineComments()
    {
        var sql = @"SELECT id -- this is a line comment
FROM users";

        var result = SqlFormatter.Format(sql);

        Assert.Contains("-- this is a line comment", result);
    }

    [Fact]
    public void Format_PreservesBlockComments()
    {
        var sql = @"SELECT /* block comment */ id FROM users";

        var result = SqlFormatter.Format(sql);

        Assert.Contains("/* block comment */", result);
    }

    [Fact]
    public void Format_PreservesSingleQuotedStrings()
    {
        var sql = @"SELECT 'Hello World' AS message FROM users";

        var result = SqlFormatter.Format(sql);

        Assert.Contains("'Hello World'", result);
    }

    [Fact]
    public void Format_PreservesDoubleQuotedStrings()
    {
        var sql = @"SELECT ""Double Quoted"" AS message FROM users";

        var result = SqlFormatter.Format(sql);

        Assert.Contains("\"Double Quoted\"", result);
    }

    [Fact]
    public void Format_PreservesSquareBracketIdentifiers()
    {
        var sql = @"SELECT [User Name], [Order Date] FROM [Sales Table]";

        var result = SqlFormatter.Format(sql);

        Assert.Contains("[User Name]", result);
        Assert.Contains("[Order Date]", result);
        Assert.Contains("[Sales Table]", result);
    }

    [Fact]
    public void Format_PreservesDoubleQuotedIdentifiers()
    {
        var sql = @"SELECT ""Column Name"" FROM ""Table Name""";

        var result = SqlFormatter.Format(sql);

        Assert.Contains("\"Column Name\"", result);
        Assert.Contains("\"Table Name\"", result);
    }

    [Fact]
    public void Format_PreservesCodeStructure_NoLayoutReformatting()
    {
        // Original indentation and line breaks should be preserved
        var sql = @"SELECT
    id,
        name,
            email
FROM users";

        var result = SqlFormatter.Format(sql);

        // Should preserve the irregular indentation pattern
        // Note: Default ColumnCasing is None, so identifiers keep their original casing
        var lines = result.Split('\n');
        Assert.Contains(lines, line => line.Contains("    id"));
        Assert.Contains(lines, line => line.Contains("        name"));
        Assert.Contains(lines, line => line.Contains("            email"));
    }

    [Fact]
    public void Format_PreservesParenthesisInternalLineBreaks()
    {
        var sql = @"SELECT id FROM users WHERE status IN (
    'active',
    'pending',
    'approved'
)";

        var result = SqlFormatter.Format(sql);

        // Line breaks within parentheses should be preserved
        Assert.Contains("(\n", result.Replace("\r\n", "\n"));
        Assert.Contains("'active',\n", result.Replace("\r\n", "\n"));
        Assert.Contains("'pending',\n", result.Replace("\r\n", "\n"));
        Assert.Contains("'approved'\n", result.Replace("\r\n", "\n"));
    }

    [Fact]
    public void Format_PreservesExpressionStructure()
    {
        var sql = @"SELECT
    id,
    (price * quantity) AS total,
    CASE
        WHEN status = 'active' THEN 1
        ELSE 0
    END AS is_active
FROM orders";

        var result = SqlFormatter.Format(sql);

        // Expression structure (CASE, parentheses, line breaks) should be preserved
        // Note: Default ColumnCasing is None, so identifiers keep their original casing
        Assert.Contains("(price * quantity)", result);
        Assert.Contains("CASE", result);
        Assert.Contains("WHEN", result);
        Assert.Contains("THEN", result);
        Assert.Contains("ELSE", result);
        Assert.Contains("END", result);

        // Verify structure is maintained (not collapsed to single line)
        var lines = result.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        Assert.True(lines.Length > 5, "Expression structure should preserve line breaks");
    }

    [Fact]
    public void Format_PreservesComplexNestedStructures()
    {
        var sql = @"/* Query to get user stats */
SELECT
    u.id,
    u.[User Name], -- quoted identifier
    'Status: ' + u.status AS status_label, -- string literal
    (
        SELECT COUNT(*)
        FROM orders o
        WHERE o.user_id = u.id
    ) AS order_count -- nested query with line breaks
FROM [User Table] u
WHERE u.status IN (
    'active',
    'premium'
)";

        var result = SqlFormatter.Format(sql);

        // All preservation rules should work together
        Assert.Contains("/* Query to get user stats */", result);
        Assert.Contains("-- quoted identifier", result);
        Assert.Contains("-- string literal", result);
        Assert.Contains("-- nested query with line breaks", result);
        Assert.Contains("[User Name]", result);
        Assert.Contains("[User Table]", result);
        Assert.Contains("'Status: '", result);
        Assert.Contains("'active'", result);
        Assert.Contains("'premium'", result);

        // Verify nested structure is preserved
        Assert.Contains("(\n", result.Replace("\r\n", "\n"));
        Assert.Contains("SELECT COUNT(*)", result);
    }

    [Fact]
    public void Format_WithInlineSpacing_PreservesSpacing()
    {
        var sql = "SELECT  id,name,  email  FROM  users";
        var result = SqlFormatter.Format(sql);

        // Default ColumnCasing/TableCasing is None, so identifiers keep original casing
        // Whitespace is preserved, only comma spacing is normalized
        Assert.Contains("SELECT  id, name,  email  FROM  users", result);
    }

    [Fact]
    public void Format_InlineSpacingWithCommaStyle_WorksTogether()
    {
        var sql = "SELECT id,name,email FROM users";
        var options = new FormattingOptions
        {
            CommaStyle = CommaStyle.Leading,
            ColumnCasing = ElementCasing.Lower
        };

        var result = SqlFormatter.Format(sql, options);

        // Should have inline spacing normalized first, then comma style applied
        Assert.Contains("SELECT id", result);
        Assert.Contains(", name", result);
        Assert.Contains(", email", result);
    }

    [Fact]
    public void Format_InlineSpacingDisabled_PreservesOriginalSpacing()
    {
        var sql = "SELECT  id,name  FROM  users";
        var options = new FormattingOptions { NormalizeInlineSpacing = false };
        var result = SqlFormatter.Format(sql, options);

        // Should preserve double spaces and missing space after comma
        // Note: Default ColumnCasing is None, so identifiers keep original casing
        Assert.Contains("  ", result);
        Assert.Contains("id,name", result);
    }

    [Fact]
    public void Format_InlineSpacingWithStringsAndComments_PreservesProtectedRegions()
    {
        var sql = @"SELECT 'a,  b',  id,  /* test,  comment */  name  -- line,  comment
FROM users";

        var result = SqlFormatter.Format(sql);

        // Protected regions should preserve their spacing
        Assert.Contains("'a,  b'", result);
        Assert.Contains("/* test,  comment */", result);
        Assert.Contains("-- line,  comment", result);

        // Whitespace is preserved (no consolidation)
        // Note: Default ColumnCasing is None, so identifiers keep original casing
        Assert.Contains("id,  /* test,  comment */  name", result);
    }
}
