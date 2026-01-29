using TsqlRefine.Formatting;
using TsqlRefine.Formatting.Helpers;
using Xunit;

namespace TsqlRefine.Formatting.Tests.Helpers;

public class ScriptDomKeywordCaserTests
{
    [Fact]
    public void Apply_KeywordCasingUpper_UppercasesKeywords()
    {
        var sql = "select id, name from users where active = 1";
        var result = ScriptDomKeywordCaser.Apply(sql, KeywordCasing.Upper, IdentifierCasing.Preserve);

        Assert.Contains("SELECT", result);
        Assert.Contains("FROM", result);
        Assert.Contains("WHERE", result);
    }

    [Fact]
    public void Apply_KeywordCasingLower_LowercasesKeywords()
    {
        var sql = "SELECT ID, NAME FROM USERS WHERE ACTIVE = 1";
        var result = ScriptDomKeywordCaser.Apply(sql, KeywordCasing.Lower, IdentifierCasing.Preserve);

        Assert.Contains("select", result);
        Assert.Contains("from", result);
        Assert.Contains("where", result);
    }

    [Fact]
    public void Apply_KeywordCasingPascal_PascalCasesKeywords()
    {
        var sql = "SELECT ID FROM USERS";
        var result = ScriptDomKeywordCaser.Apply(sql, KeywordCasing.Pascal, IdentifierCasing.Preserve);

        Assert.Contains("Select", result);
        Assert.Contains("From", result);
    }

    [Fact]
    public void Apply_IdentifierCasingUpper_UppercasesIdentifiers()
    {
        var sql = "SELECT id, name FROM users";
        var result = ScriptDomKeywordCaser.Apply(sql, KeywordCasing.Upper, IdentifierCasing.Upper);

        Assert.Contains("ID", result);
        Assert.Contains("NAME", result);
        Assert.Contains("USERS", result);
    }

    [Fact]
    public void Apply_IdentifierCasingLower_LowercasesIdentifiers()
    {
        var sql = "SELECT ID, NAME FROM USERS";
        var result = ScriptDomKeywordCaser.Apply(sql, KeywordCasing.Upper, IdentifierCasing.Lower);

        Assert.Contains("id", result);
        Assert.Contains("name", result);
        Assert.Contains("users", result);
    }

    [Fact]
    public void Apply_IdentifierCasingCamel_CamelCasesIdentifiers()
    {
        var sql = "SELECT UserID, UserName FROM Users";
        var result = ScriptDomKeywordCaser.Apply(sql, KeywordCasing.Upper, IdentifierCasing.Camel);

        Assert.Contains("userid", result);
        Assert.Contains("username", result);
        Assert.Contains("users", result);
    }

    [Fact]
    public void Apply_QuotedIdentifiers_PreservesQuotes()
    {
        var sql = "SELECT [Order], \"User\" FROM [Table Name]";
        var result = ScriptDomKeywordCaser.Apply(sql, KeywordCasing.Upper, IdentifierCasing.Lower);

        // Quoted identifiers should not be affected by IdentifierCasing
        Assert.Contains("[Order]", result);
        Assert.Contains("\"User\"", result);
        Assert.Contains("[Table Name]", result);
    }

    [Fact]
    public void Apply_Comments_PreservesComments()
    {
        var sql = "SELECT id -- this is a comment\nFROM users";
        var result = ScriptDomKeywordCaser.Apply(sql, KeywordCasing.Upper, IdentifierCasing.Preserve);

        Assert.Contains("-- this is a comment", result);
    }

    [Fact]
    public void Apply_Strings_PreservesStrings()
    {
        var sql = "SELECT 'SELECT FROM WHERE' AS text";
        var result = ScriptDomKeywordCaser.Apply(sql, KeywordCasing.Upper, IdentifierCasing.Preserve);

        // String content should not be affected
        Assert.Contains("'SELECT FROM WHERE'", result);
    }

    [Fact]
    public void Apply_CompatLevel100_UsesTSql100Parser()
    {
        var sql = "SELECT id FROM users";
        var result = ScriptDomKeywordCaser.Apply(sql, KeywordCasing.Upper, IdentifierCasing.Preserve, compatLevel: 100);

        // Should not throw and should process correctly
        Assert.Contains("SELECT", result);
    }

    [Fact]
    public void Apply_CompatLevel150_UsesTSql150Parser()
    {
        var sql = "SELECT id FROM users";
        var result = ScriptDomKeywordCaser.Apply(sql, KeywordCasing.Upper, IdentifierCasing.Preserve, compatLevel: 150);

        // Should not throw and should process correctly
        Assert.Contains("SELECT", result);
    }

    [Fact]
    public void Apply_CompatLevel160_UsesTSql160Parser()
    {
        var sql = "SELECT id FROM users";
        var result = ScriptDomKeywordCaser.Apply(sql, KeywordCasing.Upper, IdentifierCasing.Preserve, compatLevel: 160);

        // Should not throw and should process correctly
        Assert.Contains("SELECT", result);
    }

    [Fact]
    public void Apply_EmptyString_ReturnsEmpty()
    {
        var result = ScriptDomKeywordCaser.Apply("", KeywordCasing.Upper, IdentifierCasing.Preserve);
        Assert.Equal("", result);
    }

    [Fact]
    public void Apply_ComplexQuery_ProcessesCorrectly()
    {
        var sql = @"
            select u.id, u.name, o.total
            from users u
            inner join orders o on u.id = o.user_id
            where u.active = 1 and o.total > 100
            order by o.total desc";

        var result = ScriptDomKeywordCaser.Apply(sql, KeywordCasing.Upper, IdentifierCasing.Preserve);

        Assert.Contains("SELECT", result);
        Assert.Contains("FROM", result);
        Assert.Contains("INNER JOIN", result);
        Assert.Contains("ON", result);
        Assert.Contains("WHERE", result);
        Assert.Contains("AND", result);
        Assert.Contains("ORDER BY", result);
        Assert.Contains("DESC", result);
    }
}
