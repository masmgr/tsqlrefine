using TsqlRefine.Formatting;
using Xunit;

namespace TsqlRefine.Formatting.Tests;

public class SqlFormatterTests
{
    [Fact]
    public void Format_EmptyString_ReturnsEmpty()
    {
        var result = SqlFormatter.Format("");
        Assert.Equal("", result);
    }

    [Fact]
    public void Format_NullString_ReturnsEmpty()
    {
        var result = SqlFormatter.Format(null!);
        Assert.Equal("", result);
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
            KeywordCasing = KeywordCasing.Lower,
            IdentifierCasing = IdentifierCasing.Lower
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
}
