namespace TsqlRefine.Formatting.Helpers.Casing;

/// <summary>
/// Tracks parsing state for element categorization during casing operations.
/// </summary>
public sealed class CasingContext
{
    /// <summary>
    /// Whether we're currently in a FROM/JOIN clause (expecting table names).
    /// </summary>
    public bool InTableContext { get; set; }

    /// <summary>
    /// Whether we're currently after an AS keyword (expecting an alias).
    /// </summary>
    public bool AfterAsKeyword { get; set; }

    /// <summary>
    /// The last schema name seen (for system table detection).
    /// </summary>
    public string? LastSchemaName { get; set; }

    /// <summary>
    /// Whether we're currently in a procedure/function call context (right after EXEC/EXECUTE).
    /// </summary>
    public bool InExecuteContext { get; set; }

    /// <summary>
    /// Whether we've already processed the procedure name after EXEC.
    /// </summary>
    public bool ExecuteProcedureProcessed { get; set; }

    /// <summary>
    /// Whether we are inside a parenthesized column-definition/list region while in table context.
    /// Examples: CREATE TABLE (...), INSERT INTO t (...).
    /// </summary>
    public bool InTableColumnList { get; set; }

    /// <summary>
    /// Resets all context state.
    /// </summary>
    public void Reset()
    {
        InTableContext = false;
        AfterAsKeyword = false;
        LastSchemaName = null;
        InExecuteContext = false;
        ExecuteProcedureProcessed = false;
        InTableColumnList = false;
    }
}
