using System.Collections.Frozen;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace TsqlRefine.Formatting.Helpers;

/// <summary>
/// Categorizes T-SQL tokens into different element types for granular casing control.
/// Uses heuristic-based detection combining token type names and keyword lookups.
/// </summary>
public static class SqlElementCategorizer
{
    /// <summary>
    /// Common T-SQL built-in functions (aggregate, string, date, system, etc.)
    /// </summary>
    private static readonly FrozenSet<string> BuiltInFunctions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        // Aggregate functions
        "COUNT", "SUM", "AVG", "MIN", "MAX", "STDEV", "STDEVP", "VAR", "VARP",
        "CHECKSUM_AGG", "COUNT_BIG", "GROUPING", "GROUPING_ID",

        // String functions
        "LEN", "SUBSTRING", "LEFT", "RIGHT", "UPPER", "LOWER", "TRIM", "LTRIM", "RTRIM",
        "REPLACE", "REPLICATE", "REVERSE", "STUFF", "CHARINDEX", "PATINDEX",
        "CONCAT", "CONCAT_WS", "STRING_AGG", "FORMAT", "QUOTENAME", "SOUNDEX",
        "SPACE", "STR", "UNICODE", "ASCII", "CHAR", "NCHAR",

        // Date/time functions
        "GETDATE", "GETUTCDATE", "SYSDATETIME", "SYSUTCDATETIME", "SYSDATETIMEOFFSET",
        "CURRENT_TIMESTAMP", "DATEADD", "DATEDIFF", "DATEDIFF_BIG", "DATEFROMPARTS",
        "DATETIME2FROMPARTS", "DATETIMEFROMPARTS", "DATETIMEOFFSETFROMPARTS",
        "SMALLDATETIMEFROMPARTS", "TIMEFROMPARTS", "DATENAME", "DATEPART",
        "DAY", "MONTH", "YEAR", "EOMONTH", "ISDATE",

        // Conversion functions
        "CAST", "CONVERT", "PARSE", "TRY_CAST", "TRY_CONVERT", "TRY_PARSE",

        // Null handling functions
        "ISNULL", "COALESCE", "NULLIF",

        // Mathematical functions
        "ABS", "CEILING", "FLOOR", "ROUND", "POWER", "SQRT", "SQUARE", "EXP", "LOG", "LOG10",
        "SIGN", "RAND", "SIN", "COS", "TAN", "ASIN", "ACOS", "ATAN", "ATN2", "DEGREES", "RADIANS",

        // Ranking functions
        "ROW_NUMBER", "RANK", "DENSE_RANK", "NTILE", "PERCENT_RANK", "CUME_DIST",

        // System functions
        "@@IDENTITY", "@@ROWCOUNT", "@@ERROR", "@@TRANCOUNT", "@@VERSION",
        "SCOPE_IDENTITY", "IDENT_CURRENT", "IDENT_INCR", "IDENT_SEED",
        "NEWID", "NEWSEQUENTIALID", "SERVERPROPERTY", "CONNECTIONPROPERTY",
        "CURRENT_USER", "SESSION_USER", "SYSTEM_USER", "USER_NAME", "SUSER_NAME",
        "DB_ID", "DB_NAME", "OBJECT_ID", "OBJECT_NAME", "TYPE_ID", "TYPE_NAME",
        "SCHEMA_ID", "SCHEMA_NAME", "COL_LENGTH", "COL_NAME", "COLUMNPROPERTY",

        // JSON functions (SQL Server 2016+)
        "ISJSON", "JSON_VALUE", "JSON_QUERY", "JSON_MODIFY",
        "OPENJSON", "FOR JSON",

        // XML functions
        "OPENXML", "FOR XML",

        // Other functions
        "IIF", "CHOOSE", "CASE", "HASHBYTES", "CHECKSUM", "BINARY_CHECKSUM"
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// T-SQL data type keywords
    /// </summary>
    private static readonly FrozenSet<string> DataTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        // Exact numeric types
        "BIT", "TINYINT", "SMALLINT", "INT", "INTEGER", "BIGINT", "DECIMAL", "NUMERIC", "MONEY", "SMALLMONEY",

        // Approximate numeric types
        "FLOAT", "REAL",

        // Date and time types
        "DATE", "TIME", "DATETIME", "DATETIME2", "DATETIMEOFFSET", "SMALLDATETIME",

        // Character string types
        "CHAR", "VARCHAR", "TEXT",

        // Unicode character string types
        "NCHAR", "NVARCHAR", "NTEXT",

        // Binary string types
        "BINARY", "VARBINARY", "IMAGE",

        // Other data types
        "UNIQUEIDENTIFIER", "XML", "SQL_VARIANT", "CURSOR", "TABLE", "HIERARCHYID",
        "GEOMETRY", "GEOGRAPHY", "ROWVERSION", "TIMESTAMP"
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Categories for SQL token elements
    /// </summary>
    public enum ElementCategory
    {
        /// <summary>SQL keyword (SELECT, FROM, WHERE, etc.)</summary>
        Keyword,
        /// <summary>Built-in function (COUNT, SUM, GETDATE, etc.)</summary>
        BuiltInFunction,
        /// <summary>Data type (INT, VARCHAR, DATETIME, etc.)</summary>
        DataType,
        /// <summary>Schema name (dbo, sys, etc.)</summary>
        Schema,
        /// <summary>Table name or alias</summary>
        Table,
        /// <summary>Column name or alias</summary>
        Column,
        /// <summary>Variable (@var, @@var)</summary>
        Variable,
        /// <summary>Other (literals, comments, operators, etc.)</summary>
        Other
    }

    /// <summary>
    /// Context information for categorizing identifiers as schema, table, or column
    /// </summary>
    private enum IdentifierContext
    {
        Unknown,
        Schema,
        Table,
        Column
    }

    /// <summary>
    /// Tracks parsing state for categorization
    /// </summary>
    public class CasingContext
    {
        /// <summary>
        /// Whether we're currently in a FROM/JOIN clause (expecting table names)
        /// </summary>
        public bool InTableContext { get; set; }

        /// <summary>
        /// Whether we're currently after an AS keyword (expecting an alias)
        /// </summary>
        public bool AfterAsKeyword { get; set; }
    }

    /// <summary>
    /// Categorizes a T-SQL token into an element category.
    /// </summary>
    /// <param name="token">The token to categorize</param>
    /// <param name="previousToken">The previous non-trivia token (for context)</param>
    /// <param name="nextToken">The next non-trivia token (for context)</param>
    /// <param name="context">Parsing context state (tracked across tokens)</param>
    /// <returns>The element category</returns>
    public static ElementCategory Categorize(
        TSqlParserToken token,
        TSqlParserToken? previousToken,
        TSqlParserToken? nextToken,
        CasingContext? context = null)
    {
        context ??= new CasingContext();

        if (string.IsNullOrEmpty(token.Text))
        {
            UpdateContext(token, context);
            return ElementCategory.Other;
        }

        var text = token.Text;
        var typeName = token.TokenType.ToString();

        // 1. Variables (@var, @@var)
        if (typeName.Contains("Variable", StringComparison.Ordinal) ||
            text.StartsWith('@'))
        {
            UpdateContext(token, context);
            return ElementCategory.Variable;
        }

        // 2. Skip non-word tokens (operators, punctuation, literals, comments, whitespace)
        if (!IsWordToken(text) ||
            typeName.Contains("Literal", StringComparison.Ordinal) ||
            typeName.Contains("Comment", StringComparison.Ordinal) ||
            typeName.Contains("WhiteSpace", StringComparison.Ordinal) ||
            typeName.Contains("Whitespace", StringComparison.Ordinal))
        {
            UpdateContext(token, context);
            return ElementCategory.Other;
        }

        // 3. Quoted identifiers → determine from context
        if (text.StartsWith('[') || text.StartsWith('"'))
        {
            var category = CategorizeQuotedIdentifier(token, previousToken, nextToken, context);
            UpdateContext(token, context);
            return category;
        }

        // 4. Built-in functions (check before keywords, as some overlap)
        if (BuiltInFunctions.Contains(text) && IsFollowedByParenthesis(nextToken))
        {
            UpdateContext(token, context);
            return ElementCategory.BuiltInFunction;
        }

        // 5. Data types
        if (DataTypes.Contains(text))
        {
            UpdateContext(token, context);
            return ElementCategory.DataType;
        }

        // 6. Identifiers (schema.table.column, table.column, alias, etc.)
        if (typeName.Contains("Identifier", StringComparison.Ordinal))
        {
            var category = CategorizeIdentifier(token, previousToken, nextToken, context);
            UpdateContext(token, context);
            return category;
        }

        // 7. Keywords (SELECT, FROM, WHERE, etc.) - default for all other word tokens
        UpdateContext(token, context);
        return ElementCategory.Keyword;
    }

    /// <summary>
    /// Updates the parsing context based on the current token
    /// </summary>
    private static void UpdateContext(TSqlParserToken token, CasingContext context)
    {
        var text = token.Text ?? "";

        // Track when we enter table context (FROM, JOIN, INTO, UPDATE)
        if (IsTableContextKeyword(text))
        {
            context.InTableContext = true;
            context.AfterAsKeyword = false;
        }
        // Exit table context on certain keywords
        else if (IsEndOfTableContextKeyword(text))
        {
            context.InTableContext = false;
            context.AfterAsKeyword = false;
        }
        // Track AS keyword
        else if (text.Equals("AS", StringComparison.OrdinalIgnoreCase))
        {
            context.AfterAsKeyword = true;
        }
        // After processing an identifier following AS, reset the flag
        else if (context.AfterAsKeyword && token.TokenType.ToString().Contains("Identifier"))
        {
            context.AfterAsKeyword = false;
        }
    }

    /// <summary>
    /// Checks if a keyword ends table context (WHERE, GROUP, ORDER, HAVING, etc.)
    /// </summary>
    private static bool IsEndOfTableContextKeyword(string text)
    {
        return text.Equals("WHERE", StringComparison.OrdinalIgnoreCase) ||
               text.Equals("GROUP", StringComparison.OrdinalIgnoreCase) ||
               text.Equals("ORDER", StringComparison.OrdinalIgnoreCase) ||
               text.Equals("HAVING", StringComparison.OrdinalIgnoreCase) ||
               text.Equals("SELECT", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Categorizes a quoted identifier based on context
    /// </summary>
    private static ElementCategory CategorizeQuotedIdentifier(
        TSqlParserToken token,
        TSqlParserToken? previousToken,
        TSqlParserToken? nextToken,
        CasingContext context)
    {
        var identContext = DetermineIdentifierContext(previousToken, nextToken, context);
        return identContext switch
        {
            IdentifierContext.Schema => ElementCategory.Schema,
            IdentifierContext.Table => ElementCategory.Table,
            IdentifierContext.Column => ElementCategory.Column,
            _ => ElementCategory.Column // Default to column for safety
        };
    }

    /// <summary>
    /// Categorizes an unquoted identifier based on context
    /// </summary>
    private static ElementCategory CategorizeIdentifier(
        TSqlParserToken token,
        TSqlParserToken? previousToken,
        TSqlParserToken? nextToken,
        CasingContext context)
    {
        var identContext = DetermineIdentifierContext(previousToken, nextToken, context);
        return identContext switch
        {
            IdentifierContext.Schema => ElementCategory.Schema,
            IdentifierContext.Table => ElementCategory.Table,
            IdentifierContext.Column => ElementCategory.Column,
            _ => ElementCategory.Column // Default to column
        };
    }

    /// <summary>
    /// Determines whether an identifier is a schema, table, or column based on context
    /// </summary>
    private static IdentifierContext DetermineIdentifierContext(
        TSqlParserToken? previousToken,
        TSqlParserToken? nextToken,
        CasingContext context)
    {
        var prevText = previousToken?.Text ?? "";
        var nextText = nextToken?.Text ?? "";

        // Schema context: identifier followed by dot
        // Example: dbo. | sys. | staging.
        if (nextText == ".")
        {
            return IdentifierContext.Schema;
        }

        // Table context:
        // 1. We're in a FROM/JOIN clause (tracked by context)
        // 2. Identifier preceded by FROM/JOIN/INTO/UPDATE
        // 3. Identifier preceded by dot (after schema)
        // Examples: FROM users, orders | JOIN Orders | FROM dbo.Users
        if (context.InTableContext ||
            IsTableContextKeyword(prevText) ||
            prevText == ".")
        {
            return IdentifierContext.Table;
        }

        // Column context: everything else (most common case)
        // Examples: SELECT id, name | u.UserId | AS OrderCount
        return IdentifierContext.Column;
    }

    /// <summary>
    /// Checks if the previous token indicates a table context
    /// </summary>
    private static bool IsTableContextKeyword(string text)
    {
        return text.Equals("FROM", StringComparison.OrdinalIgnoreCase) ||
               text.Equals("JOIN", StringComparison.OrdinalIgnoreCase) ||
               text.Equals("INTO", StringComparison.OrdinalIgnoreCase) ||
               text.Equals("UPDATE", StringComparison.OrdinalIgnoreCase) ||
               text.Equals("TABLE", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks if the next token is an opening parenthesis (function call indicator)
    /// </summary>
    private static bool IsFollowedByParenthesis(TSqlParserToken? nextToken)
    {
        return nextToken?.Text == "(";
    }

    /// <summary>
    /// Checks if text is a word token (starts with letter, contains only letters/digits/underscore)
    /// </summary>
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
