using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Helpers.Analysis;

/// <summary>
/// Helper utilities for analyzing script statements in order.
/// </summary>
public static class ScriptStatementAnalysisHelpers
{
    /// <summary>
    /// Determines whether statement-order preamble checks should be enforced for the script.
    /// Enforces when the script contains CREATE PROCEDURE/FUNCTION, or has at least the
    /// minimum number of statements.
    /// </summary>
    /// <param name="script">The script to inspect.</param>
    /// <param name="minimumStatementsWithoutCreate">
    /// The minimum number of statements required when no CREATE PROCEDURE/FUNCTION exists.
    /// </param>
    /// <returns>True when preamble checks should run; otherwise false.</returns>
    /// <exception cref="ArgumentNullException">Thrown when script is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when minimumStatementsWithoutCreate is less than 1.
    /// </exception>
    public static bool ShouldEnforcePreambleChecks(
        TSqlScript script,
        int minimumStatementsWithoutCreate = 3)
    {
        ArgumentNullException.ThrowIfNull(script);

        ArgumentOutOfRangeException.ThrowIfLessThan(minimumStatementsWithoutCreate, 1);

        if (script.Batches.Count == 0)
        {
            return false;
        }

        var hasCreateStatement = false;
        var totalStatements = 0;

        foreach (var batch in script.Batches)
        {
            foreach (var statement in batch.Statements)
            {
                totalStatements++;
                if (statement is
                    CreateProcedureStatement or
                    CreateOrAlterProcedureStatement or
                    CreateFunctionStatement or
                    CreateOrAlterFunctionStatement)
                {
                    hasCreateStatement = true;
                    break;
                }
            }

            if (hasCreateStatement)
            {
                break;
            }
        }

        return hasCreateStatement || totalStatements >= minimumStatementsWithoutCreate;
    }

    /// <summary>
    /// Checks whether any statement in the first N statements matches the predicate.
    /// </summary>
    /// <param name="script">The script to inspect.</param>
    /// <param name="maxStatements">Maximum number of leading statements to inspect.</param>
    /// <param name="predicate">Predicate to evaluate for each statement.</param>
    /// <returns>True when a matching statement is found within the first N statements.</returns>
    /// <exception cref="ArgumentNullException">Thrown when script or predicate is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when maxStatements is less than 1.</exception>
    public static bool AnyInFirstStatements(
        TSqlScript script,
        int maxStatements,
        Func<TSqlStatement, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(script);
        ArgumentNullException.ThrowIfNull(predicate);

        ArgumentOutOfRangeException.ThrowIfLessThan(maxStatements, 1);

        var statementCount = 0;

        foreach (var batch in script.Batches)
        {
            foreach (var statement in batch.Statements)
            {
                statementCount++;

                if (predicate(statement))
                {
                    return true;
                }

                if (statementCount >= maxStatements)
                {
                    return false;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Creates a file-level diagnostic at the start of the file.
    /// </summary>
    /// <param name="metadata">The rule metadata.</param>
    /// <param name="message">The diagnostic message.</param>
    /// <returns>A diagnostic anchored at line 0, column 0.</returns>
    /// <exception cref="ArgumentNullException">Thrown when metadata or message is null.</exception>
    public static Diagnostic CreateFileStartDiagnostic(RuleMetadata metadata, string message)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(message);

        return new Diagnostic(
            Range: new TsqlRefine.PluginSdk.Range(
                new Position(0, 0),
                new Position(0, 0)),
            Message: message,
            Severity: null,
            Code: metadata.RuleId,
            Data: new DiagnosticData(metadata.RuleId, metadata.Category, metadata.Fixable));
    }
}
