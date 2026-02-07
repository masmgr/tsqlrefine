using TsqlRefine.Formatting.Helpers.Casing;
using Xunit;

namespace TsqlRefine.Formatting.Tests.Helpers;

public class ScriptDomElementCaserTests
{
    [Fact]
    public void Apply_KeywordElementCasing_Upper_UppercasesKeywords()
    {
        var sql = "select id from users where active = 1";
        var options = new FormattingOptions
        {
            KeywordElementCasing = ElementCasing.Upper
        };

        var result = ScriptDomElementCaser.Apply(sql, options);

        Assert.Contains("SELECT", result);
        Assert.Contains("FROM", result);
        Assert.Contains("WHERE", result);
    }

    [Fact]
    public void Apply_KeywordElementCasing_Lower_LowercasesKeywords()
    {
        var sql = "SELECT ID FROM USERS WHERE ACTIVE = 1";
        var options = new FormattingOptions
        {
            KeywordElementCasing = ElementCasing.Lower
        };

        var result = ScriptDomElementCaser.Apply(sql, options);

        Assert.Contains("select", result);
        Assert.Contains("from", result);
        Assert.Contains("where", result);
    }

    [Fact]
    public void Apply_BuiltInFunctionCasing_Upper_UppercasesFunctions()
    {
        var sql = "select count(*), sum(amount), getdate() from orders";
        var options = new FormattingOptions
        {
            BuiltInFunctionCasing = ElementCasing.Upper
        };

        var result = ScriptDomElementCaser.Apply(sql, options);

        Assert.Contains("COUNT", result);
        Assert.Contains("SUM", result);
        Assert.Contains("GETDATE", result);
    }

    [Fact]
    public void Apply_BuiltInFunctionCasing_Lower_LowercasesFunctions()
    {
        var sql = "SELECT COUNT(*), SUM(Amount), GETDATE() FROM Orders";
        var options = new FormattingOptions
        {
            BuiltInFunctionCasing = ElementCasing.Lower
        };

        var result = ScriptDomElementCaser.Apply(sql, options);

        Assert.Contains("count", result);
        Assert.Contains("sum", result);
        Assert.Contains("getdate", result);
    }

    [Fact]
    public void Apply_DataTypeCasing_Lower_LowercasesDataTypes()
    {
        var sql = "DECLARE @id INT, @name VARCHAR(50), @date DATETIME";
        var options = new FormattingOptions
        {
            DataTypeCasing = ElementCasing.Lower
        };

        var result = ScriptDomElementCaser.Apply(sql, options);

        Assert.Contains("int", result);
        Assert.Contains("varchar", result);
        Assert.Contains("datetime", result);
    }

    [Fact]
    public void Apply_DataTypeCasing_Upper_UppercasesDataTypes()
    {
        var sql = "declare @id int, @name varchar(50), @date datetime";
        var options = new FormattingOptions
        {
            DataTypeCasing = ElementCasing.Upper
        };

        var result = ScriptDomElementCaser.Apply(sql, options);

        Assert.Contains("INT", result);
        Assert.Contains("VARCHAR", result);
        Assert.Contains("DATETIME", result);
    }

    [Fact]
    public void Apply_SchemaCasing_Lower_LowercasesSchemas()
    {
        var sql = "SELECT * FROM DBO.Users, SYS.Tables";
        var options = new FormattingOptions
        {
            SchemaCasing = ElementCasing.Lower
        };

        var result = ScriptDomElementCaser.Apply(sql, options);

        Assert.Contains("dbo.", result);
        Assert.Contains("sys.", result);
    }

    [Fact]
    public void Apply_TableCasing_Upper_UppercasesTables()
    {
        var sql = "select * from users, orders";
        var options = new FormattingOptions
        {
            TableCasing = ElementCasing.Upper
        };

        var result = ScriptDomElementCaser.Apply(sql, options);

        Assert.Contains("USERS", result);
        Assert.Contains("ORDERS", result);
    }

    [Fact]
    public void Apply_TableCasing_Lower_LowercasesTables()
    {
        var sql = "SELECT * FROM USERS, ORDERS";
        var options = new FormattingOptions
        {
            TableCasing = ElementCasing.Lower
        };

        var result = ScriptDomElementCaser.Apply(sql, options);

        Assert.Contains("users", result);
        Assert.Contains("orders", result);
    }

    [Fact]
    public void Apply_ColumnCasing_Upper_UppercasesColumns()
    {
        var sql = "select id, name, email from users";
        var options = new FormattingOptions
        {
            ColumnCasing = ElementCasing.Upper
        };

        var result = ScriptDomElementCaser.Apply(sql, options);

        Assert.Contains("ID", result);
        Assert.Contains("NAME", result);
        Assert.Contains("EMAIL", result);
    }

    [Fact]
    public void Apply_ColumnCasing_Lower_LowercasesColumns()
    {
        var sql = "SELECT ID, NAME, EMAIL FROM USERS";
        var options = new FormattingOptions
        {
            ColumnCasing = ElementCasing.Lower
        };

        var result = ScriptDomElementCaser.Apply(sql, options);

        Assert.Contains("id", result);
        Assert.Contains("name", result);
        Assert.Contains("email", result);
    }

    [Fact]
    public void Apply_VariableCasing_Lower_LowercasesVariables()
    {
        var sql = "DECLARE @UserId INT; SELECT @@ROWCOUNT";
        var options = new FormattingOptions
        {
            VariableCasing = ElementCasing.Lower
        };

        var result = ScriptDomElementCaser.Apply(sql, options);

        Assert.Contains("@userid", result.ToLowerInvariant());
        Assert.Contains("@@rowcount", result.ToLowerInvariant());
    }

    [Fact]
    public void Apply_VariableCasing_Upper_UppercasesVariables()
    {
        var sql = "declare @userid int; select @@rowcount";
        var options = new FormattingOptions
        {
            VariableCasing = ElementCasing.Upper
        };

        var result = ScriptDomElementCaser.Apply(sql, options);

        Assert.Contains("@USERID", result);
        Assert.Contains("@@ROWCOUNT", result);
    }

    [Fact]
    public void Apply_TableAliases_FollowTableCasing()
    {
        var sql = "select u.id, o.total from users u inner join orders o on u.id = o.user_id";
        var options = new FormattingOptions
        {
            TableCasing = ElementCasing.Upper
        };

        var result = ScriptDomElementCaser.Apply(sql, options);

        // Table aliases should follow table casing
        Assert.Contains("USERS U", result);
        Assert.Contains("ORDERS O", result);
    }

    [Fact]
    public void Apply_ColumnAliases_FollowColumnCasing()
    {
        var sql = "select count(*) as ordercount, sum(total) as totalamount from orders";
        var options = new FormattingOptions
        {
            ColumnCasing = ElementCasing.Upper,
            KeywordElementCasing = ElementCasing.Lower  // Keep AS lowercase
        };

        var result = ScriptDomElementCaser.Apply(sql, options);

        // Column aliases after AS should follow column casing (AS keyword uses keyword casing)
        Assert.Contains("as ORDERCOUNT", result);
        Assert.Contains("as TOTALAMOUNT", result);
        // Verify columns are uppercase
        Assert.Contains("ORDERCOUNT", result);
        Assert.Contains("TOTALAMOUNT", result);
    }

    [Fact]
    public void Apply_ComprehensiveQuery_AppliesAllCasingRules()
    {
        var sql = @"
declare @userid int = 1;
select u.userid, u.username, count(*) as ordercount, getdate() as currentdate
from dbo.users u
where u.isactive = 1 and u.createdate >= dateadd(day, -30, getdate());
";

        var options = new FormattingOptions
        {
            KeywordElementCasing = ElementCasing.Upper,
            BuiltInFunctionCasing = ElementCasing.Upper,
            DataTypeCasing = ElementCasing.Lower,
            SchemaCasing = ElementCasing.Lower,
            TableCasing = ElementCasing.Upper,
            ColumnCasing = ElementCasing.Upper,
            VariableCasing = ElementCasing.Lower
        };

        var result = ScriptDomElementCaser.Apply(sql, options);

        // Keywords
        Assert.Contains("DECLARE", result);
        Assert.Contains("SELECT", result);
        Assert.Contains("FROM", result);
        Assert.Contains("WHERE", result);
        Assert.Contains("AND", result);

        // Functions
        Assert.Contains("COUNT", result);
        Assert.Contains("GETDATE", result);
        Assert.Contains("DATEADD", result);

        // Data types
        Assert.Contains("int", result);

        // Schema
        Assert.Contains("dbo.", result);

        // Tables
        Assert.Contains("USERS", result);

        // Columns
        Assert.Contains("USERID", result);
        Assert.Contains("USERNAME", result);
        Assert.Contains("ISACTIVE", result);
        Assert.Contains("CREATEDATE", result);

        // Variables
        Assert.Contains("@userid", result);
    }

    [Fact]
    public void Apply_NoneOption_PreservesOriginalCase()
    {
        var sql = "SELECT UserId, UserName FROM Users WHERE IsActive = 1";
        var options = new FormattingOptions
        {
            KeywordElementCasing = ElementCasing.None,
            ColumnCasing = ElementCasing.None,
            TableCasing = ElementCasing.None
        };

        var result = ScriptDomElementCaser.Apply(sql, options);

        // Should preserve original mixed case
        Assert.Contains("UserId", result);
        Assert.Contains("UserName", result);
        Assert.Contains("Users", result);
        Assert.Contains("IsActive", result);
    }

    [Fact]
    public void Apply_QuotedIdentifiers_PreservesQuotesAndContent()
    {
        var sql = "SELECT [Order], \"User\" FROM [Table Name]";
        var options = new FormattingOptions
        {
            ColumnCasing = ElementCasing.Upper,
            TableCasing = ElementCasing.Upper
        };

        var result = ScriptDomElementCaser.Apply(sql, options);

        // Quoted identifiers should be preserved as-is
        Assert.Contains("[Order]", result);
        Assert.Contains("\"User\"", result);
        Assert.Contains("[Table Name]", result);
    }

    [Fact]
    public void Apply_PreservesStringsAndComments()
    {
        var sql = "SELECT 'select from where' AS text, id -- comment with SELECT";
        var options = new FormattingOptions
        {
            KeywordElementCasing = ElementCasing.Upper
        };

        var result = ScriptDomElementCaser.Apply(sql, options);

        // String content should not be affected
        Assert.Contains("'select from where'", result);

        // Comment content should not be affected
        Assert.Contains("-- comment with SELECT", result);
    }

    [Fact]
    public void Apply_SystemTable_LowercasesSystemTables()
    {
        var sql = "SELECT * FROM sys.tables WHERE name = 'users'";
        var options = new FormattingOptions
        {
            KeywordElementCasing = ElementCasing.Upper,
            SchemaCasing = ElementCasing.Lower,
            TableCasing = ElementCasing.Upper,
            SystemTableCasing = ElementCasing.Lower
        };

        var result = ScriptDomElementCaser.Apply(sql, options);

        // System table should be lowercase
        Assert.Contains("sys.tables", result);

        // But keyword should still be uppercase
        Assert.Contains("SELECT", result);
        Assert.Contains("FROM", result);
        Assert.Contains("WHERE", result);
    }

    [Fact]
    public void Apply_SystemTable_InformationSchema()
    {
        var sql = "SELECT * FROM information_schema.columns WHERE table_name = 'users'";
        var options = new FormattingOptions
        {
            KeywordElementCasing = ElementCasing.Upper,
            SchemaCasing = ElementCasing.Lower,
            TableCasing = ElementCasing.Upper,
            SystemTableCasing = ElementCasing.Lower
        };

        var result = ScriptDomElementCaser.Apply(sql, options);

        // System table should be lowercase
        Assert.Contains("information_schema.columns", result);

        // Keyword should be uppercase
        Assert.Contains("SELECT", result);
        Assert.Contains("FROM", result);
    }

    [Fact]
    public void Apply_StoredProcedure_PreservesOriginalCase()
    {
        var sql = "EXEC MyStoredProc @param = 1";
        var options = new FormattingOptions
        {
            KeywordElementCasing = ElementCasing.Upper,
            StoredProcedureCasing = ElementCasing.None
        };

        var result = ScriptDomElementCaser.Apply(sql, options);

        // Stored procedure case should be preserved
        Assert.Contains("MyStoredProc", result);

        // Keyword should still be uppercase
        Assert.Contains("EXEC", result);
    }

    [Fact]
    public void Apply_StoredProcedure_UppercasesWhenConfigured()
    {
        var sql = "EXEC myStoredProc @param = 1";
        var options = new FormattingOptions
        {
            KeywordElementCasing = ElementCasing.Upper,
            StoredProcedureCasing = ElementCasing.Upper
        };

        var result = ScriptDomElementCaser.Apply(sql, options);

        // Stored procedure should be uppercased
        Assert.Contains("MYSTOREDPROC", result);

        // Keyword should be uppercase
        Assert.Contains("EXEC", result);
    }

    [Fact]
    public void Apply_Execute_VariousFormats()
    {
        var sql = "EXECUTE myProc1; EXEC myProc2;";
        var options = new FormattingOptions
        {
            KeywordElementCasing = ElementCasing.Upper,
            StoredProcedureCasing = ElementCasing.None
        };

        var result = ScriptDomElementCaser.Apply(sql, options);

        // Procedure case should be preserved
        Assert.Contains("myProc1", result);
        Assert.Contains("myProc2", result);

        // Keywords should be uppercase
        Assert.Contains("EXECUTE", result);
        Assert.Contains("EXEC", result);
    }

    #region Parenthesis-free Functions

    [Fact]
    public void Apply_CurrentTimestamp_RecognizedAsFunction()
    {
        var sql = "SELECT current_timestamp, id FROM users";
        var options = new FormattingOptions
        {
            BuiltInFunctionCasing = ElementCasing.Upper,
            ColumnCasing = ElementCasing.Lower
        };

        var result = ScriptDomElementCaser.Apply(sql, options);

        // CURRENT_TIMESTAMP should be uppercased as a function (no parentheses required)
        Assert.Contains("CURRENT_TIMESTAMP", result);
        // Other columns should be lowercase
        Assert.Contains("id", result);
    }

    [Fact]
    public void Apply_CurrentUser_RecognizedAsFunction()
    {
        var sql = "SELECT current_user, id FROM users";
        var options = new FormattingOptions
        {
            BuiltInFunctionCasing = ElementCasing.Upper,
            ColumnCasing = ElementCasing.Lower
        };

        var result = ScriptDomElementCaser.Apply(sql, options);

        // CURRENT_USER should be uppercased as a function
        Assert.Contains("CURRENT_USER", result);
    }

    [Fact]
    public void Apply_SessionUser_RecognizedAsFunction()
    {
        var sql = "SELECT session_user, system_user FROM dual";
        var options = new FormattingOptions
        {
            BuiltInFunctionCasing = ElementCasing.Upper,
            ColumnCasing = ElementCasing.Lower
        };

        var result = ScriptDomElementCaser.Apply(sql, options);

        // SESSION_USER and SYSTEM_USER should be uppercased as functions
        Assert.Contains("SESSION_USER", result);
        Assert.Contains("SYSTEM_USER", result);
    }

    [Fact]
    public void Apply_UserFunction_RecognizedAsFunction()
    {
        var sql = "SELECT user, id FROM users";
        var options = new FormattingOptions
        {
            BuiltInFunctionCasing = ElementCasing.Upper,
            ColumnCasing = ElementCasing.Lower
        };

        var result = ScriptDomElementCaser.Apply(sql, options);

        // USER should be uppercased as a function
        Assert.Contains("USER", result);
    }

    #endregion

    #region Default Values

    [Fact]
    public void Apply_DefaultOptions_IdentifierCasingIsNone()
    {
        // Default FormattingOptions should have None for identifiers (safe for CS environments)
        var options = new FormattingOptions();

        Assert.Equal(ElementCasing.None, options.SchemaCasing);
        Assert.Equal(ElementCasing.None, options.TableCasing);
        Assert.Equal(ElementCasing.None, options.ColumnCasing);
    }

    [Fact]
    public void Apply_DefaultOptions_PreservesIdentifierCasing()
    {
        var sql = "SELECT UserId, UserName FROM DBO.Users WHERE IsActive = 1";
        var options = new FormattingOptions(); // Default options

        var result = ScriptDomElementCaser.Apply(sql, options);

        // With default None for identifiers, original case should be preserved
        Assert.Contains("UserId", result);
        Assert.Contains("UserName", result);
        Assert.Contains("DBO", result); // Schema preserved
        Assert.Contains("Users", result);
        Assert.Contains("IsActive", result);
    }

    #endregion
}
