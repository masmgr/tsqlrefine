using System.Collections.Frozen;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.Formatting.Helpers.Registries;

namespace TsqlRefine.Formatting.Helpers.Casing;

/// <summary>
/// Categorizes T-SQL tokens into different element types for granular casing control.
/// Uses heuristic-based detection combining token type names and keyword lookups.
/// </summary>
public static class SqlElementCategorizer
{
    /// <summary>
    /// Categories for token type classification (internal optimization).
    /// </summary>
    private enum TokenTypeCategory
    {
        Other,
        Variable,
        Literal,
        Comment,
        WhiteSpace,
        Identifier
    }

    private static readonly FrozenDictionary<TSqlTokenType, TokenTypeCategory> TokenTypeCategoryCache = BuildTokenTypeCategoryCache();

    private static readonly FrozenSet<string> TableContextKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "FROM", "JOIN", "INTO", "UPDATE", "TABLE"
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenSet<string> EndOfTableContextKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "WHERE", "GROUP", "ORDER", "HAVING", "SELECT", "ON", "SET", "VALUES"
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private static FrozenDictionary<TSqlTokenType, TokenTypeCategory> BuildTokenTypeCategoryCache()
    {
        var cache = new Dictionary<TSqlTokenType, TokenTypeCategory>();
        foreach (var tokenType in Enum.GetValues<TSqlTokenType>())
        {
            var name = tokenType.ToString();
            TokenTypeCategory category;

            if (name.Contains("Variable", StringComparison.Ordinal))
            {
                category = TokenTypeCategory.Variable;
            }
            else if (name.Contains("Literal", StringComparison.Ordinal))
            {
                category = TokenTypeCategory.Literal;
            }
            else if (name.Contains("Comment", StringComparison.Ordinal))
            {
                category = TokenTypeCategory.Comment;
            }
            else if (name.Contains("WhiteSpace", StringComparison.Ordinal) ||
                     name.Contains("Whitespace", StringComparison.Ordinal))
            {
                category = TokenTypeCategory.WhiteSpace;
            }
            else if (name.Contains("Identifier", StringComparison.Ordinal))
            {
                category = TokenTypeCategory.Identifier;
            }
            else
            {
                category = TokenTypeCategory.Other;
            }

            cache[tokenType] = category;
        }

        return cache.ToFrozenDictionary();
    }

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
        /// <summary>System table (sys.*, information_schema.*, etc.)</summary>
        SystemTable,
        /// <summary>Stored procedure name</summary>
        StoredProcedure,
        /// <summary>User-defined function name</summary>
        UserDefinedFunction,
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
        var category = CategorizeCore(token, previousToken, nextToken, context);
        UpdateContext(token, context);
        return category;
    }

    private static ElementCategory CategorizeCore(
        TSqlParserToken token,
        TSqlParserToken? previousToken,
        TSqlParserToken? nextToken,
        CasingContext context)
    {
        if (string.IsNullOrEmpty(token.Text))
        {
            return ElementCategory.Other;
        }

        var text = token.Text;
        var tokenCategory = TokenTypeCategoryCache.GetValueOrDefault(token.TokenType, TokenTypeCategory.Other);

        // 1. Variables (@var, @@var)
        if (tokenCategory == TokenTypeCategory.Variable || text.StartsWith('@'))
        {
            return ElementCategory.Variable;
        }

        // 2. Skip non-word tokens (operators, punctuation, literals, comments, whitespace)
        if (!IsWordToken(text) ||
            tokenCategory is TokenTypeCategory.Literal or TokenTypeCategory.Comment or TokenTypeCategory.WhiteSpace)
        {
            return ElementCategory.Other;
        }

        // 3. Quoted identifiers -> determine from context
        if (text.StartsWith('[') || text.StartsWith('"'))
        {
            return CategorizeQuotedIdentifier(previousToken, nextToken, context);
        }

        // 4. Built-in functions (check before keywords, as some overlap)
        // Check for parenthesis-free functions first (e.g., CURRENT_TIMESTAMP)
        if (BuiltInFunctionRegistry.IsParenthesisFreeFunction(text))
        {
            return ElementCategory.BuiltInFunction;
        }

        // Check for regular built-in functions that require parentheses
        if (BuiltInFunctionRegistry.IsBuiltInFunction(text) && IsFollowedByParenthesis(nextToken))
        {
            return ElementCategory.BuiltInFunction;
        }

        // 5. Data types
        if (DataTypeRegistry.IsDataType(text))
        {
            return ElementCategory.DataType;
        }

        // 6. Identifiers (schema.table.column, table.column, alias, etc.)
        if (tokenCategory == TokenTypeCategory.Identifier)
        {
            var category = CategorizeIdentifier(previousToken, nextToken, context);

            // Track schema name if this is a schema identifier
            if (category == ElementCategory.Schema)
            {
                context.LastSchemaName = text;
            }

            // Mark that we've processed the procedure name if in EXEC context
            if (category == ElementCategory.StoredProcedure)
            {
                context.ExecuteProcedureProcessed = true;
            }

            return category;
        }

        // 7. Keywords (SELECT, FROM, WHERE, etc.) - default for all other word tokens
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
            context.InTableColumnList = false;
            context.AfterAsKeyword = false;
            context.InExecuteContext = false;
            context.ExecuteProcedureProcessed = false;
            context.LastSchemaName = null;
        }
        // Exit table context on certain keywords
        else if (IsEndOfTableContextKeyword(text))
        {
            context.InTableContext = false;
            context.InTableColumnList = false;
            context.AfterAsKeyword = false;
            context.InExecuteContext = false;
            context.ExecuteProcedureProcessed = false;
            context.LastSchemaName = null;
        }
        // Track parenthesized column lists while in table context
        // Examples: CREATE TABLE (...), INSERT INTO t (...)
        else if (context.InTableContext && text.Equals("(", StringComparison.Ordinal))
        {
            context.InTableColumnList = true;
        }
        else if (context.InTableContext && text.Equals(")", StringComparison.Ordinal))
        {
            context.InTableColumnList = false;
        }
        // Track AS keyword
        else if (text.Equals("AS", StringComparison.OrdinalIgnoreCase))
        {
            context.AfterAsKeyword = true;
        }
        // After processing an identifier following AS, reset the flag
        else if (context.AfterAsKeyword &&
                 TokenTypeCategoryCache.GetValueOrDefault(token.TokenType) == TokenTypeCategory.Identifier)
        {
            context.AfterAsKeyword = false;
        }
        // Track EXEC/EXECUTE for procedure calls
        else if (text.Equals("EXEC", StringComparison.OrdinalIgnoreCase) ||
                 text.Equals("EXECUTE", StringComparison.OrdinalIgnoreCase))
        {
            context.InExecuteContext = true;
            context.ExecuteProcedureProcessed = false;
        }
        // Reset on statement terminators
        else if (text.Equals(";", StringComparison.Ordinal))
        {
            context.InExecuteContext = false;
            context.ExecuteProcedureProcessed = false;
            context.LastSchemaName = null;
            context.InTableColumnList = false;
        }
    }

    /// <summary>
    /// Checks if a keyword ends table context (WHERE, GROUP, ORDER, HAVING, etc.)
    /// </summary>
    private static bool IsEndOfTableContextKeyword(string text) => EndOfTableContextKeywords.Contains(text);

    /// <summary>
    /// Categorizes a quoted identifier based on context
    /// </summary>
    private static ElementCategory CategorizeQuotedIdentifier(
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
        TSqlParserToken? previousToken,
        TSqlParserToken? nextToken,
        CasingContext context)
    {
        // Handle EXEC/EXECUTE multipart procedure names:
        // EXEC proc
        // EXEC schema.proc
        // Qualifiers should follow SchemaCasing and the final identifier should
        // follow StoredProcedureCasing.
        if (TryCategorizeExecuteIdentifier(previousToken, nextToken, context, out var executeCategory))
        {
            return executeCategory;
        }

        var identContext = DetermineIdentifierContext(previousToken, nextToken, context);

        // Return mapped context category
        var baseCategory = identContext switch
        {
            IdentifierContext.Schema => ElementCategory.Schema,
            IdentifierContext.Table => ElementCategory.Table,
            IdentifierContext.Column => ElementCategory.Column,
            _ => ElementCategory.Column // Default to column
        };

        // Check if this is a system table (after sys or information_schema)
        if (baseCategory == ElementCategory.Table && SystemSchemaRegistry.IsSystemSchema(context.LastSchemaName))
        {
            return ElementCategory.SystemTable;
        }

        return baseCategory;
    }

    private static bool TryCategorizeExecuteIdentifier(
        TSqlParserToken? previousToken,
        TSqlParserToken? nextToken,
        CasingContext context,
        out ElementCategory category)
    {
        category = ElementCategory.Other;

        if (!context.InExecuteContext || context.ExecuteProcedureProcessed)
        {
            return false;
        }

        var prevText = previousToken?.Text ?? "";
        var nextText = nextToken?.Text ?? "";

        // Qualifier in multipart name (schema., db., etc.)
        if (nextText == ".")
        {
            category = ElementCategory.Schema;
            return true;
        }

        // Final identifier in multipart name
        if (prevText == ".")
        {
            category = ElementCategory.StoredProcedure;
            return true;
        }

        // Single identifier procedure name immediately after EXEC/EXECUTE
        if (prevText.Equals("EXEC", StringComparison.OrdinalIgnoreCase) ||
            prevText.Equals("EXECUTE", StringComparison.OrdinalIgnoreCase))
        {
            category = ElementCategory.StoredProcedure;
            return true;
        }

        // Conservative fallback for edge tokenization cases.
        category = ElementCategory.StoredProcedure;
        return true;
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

        // Parenthesized column lists in table context should be treated as columns.
        if (context.InTableColumnList)
        {
            return IdentifierContext.Column;
        }

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
            IsTableContextKeyword(prevText))
        {
            return IdentifierContext.Table;
        }

        // Identifier after dot is typically a column outside table context
        // (e.g., u.UserId in SELECT/WHERE clauses).
        if (prevText == ".")
        {
            return IdentifierContext.Column;
        }

        // Column context: everything else (most common case)
        // Examples: SELECT id, name | u.UserId | AS OrderCount
        return IdentifierContext.Column;
    }

    /// <summary>
    /// Checks if the previous token indicates a table context
    /// </summary>
    private static bool IsTableContextKeyword(string text) => TableContextKeywords.Contains(text);

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
