using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace TsqlRefine.Rules.Helpers;

/// <summary>
/// Helper methods for BEGIN/END block requirement rules.
/// </summary>
public static class BeginEndHelpers
{
    /// <summary>
    /// Checks if a statement is a control-flow statement (RETURN, BREAK, CONTINUE, THROW).
    /// </summary>
    /// <param name="statement">The statement to check.</param>
    /// <returns>True if the statement is a control-flow statement; otherwise, false.</returns>
    public static bool IsControlFlowStatement(TSqlStatement? statement)
        => statement is ReturnStatement or BreakStatement or ContinueStatement or ThrowStatement;

    /// <summary>
    /// Checks if a statement needs to be wrapped in BEGIN/END.
    /// </summary>
    /// <param name="statement">The statement to check.</param>
    /// <param name="allowControlFlowWithoutBlock">
    /// If true, control-flow statements (RETURN, BREAK, CONTINUE, THROW) are allowed without BEGIN/END.
    /// </param>
    /// <returns>True if the statement needs a BEGIN/END block; otherwise, false.</returns>
    public static bool NeedsBeginEndBlock(
        TSqlStatement? statement,
        bool allowControlFlowWithoutBlock = false)
    {
        if (statement is null)
        {
            return false;
        }

        if (statement is BeginEndBlockStatement)
        {
            return false;
        }

        if (allowControlFlowWithoutBlock && IsControlFlowStatement(statement))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Checks if an ELSE statement is actually an ELSE IF pattern.
    /// </summary>
    /// <param name="elseStatement">The ELSE statement to check.</param>
    /// <returns>True if the statement is an IF statement (ELSE IF pattern); otherwise, false.</returns>
    public static bool IsElseIfPattern(TSqlStatement? elseStatement)
        => elseStatement is IfStatement;
}
