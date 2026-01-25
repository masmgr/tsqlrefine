using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers;

namespace TsqlRefine.Rules.Rules;

public sealed class SetTransactionIsolationLevelRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "set-transaction-isolation-level",
        Description: "Files should start with SET TRANSACTION ISOLATION LEVEL within the first 10 statements.",
        Category: "Configuration",
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: false
    );

    public IEnumerable<Diagnostic> Analyze(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Ast.Fragment is not TSqlScript script || script.Batches.Count == 0)
        {
            yield break;
        }

        // Only check files with CREATE PROCEDURE/FUNCTION or multiple statements
        var hasCreateStatement = false;
        var totalStatements = 0;

        foreach (var batch in script.Batches)
        {
            foreach (var statement in batch.Statements)
            {
                totalStatements++;
                if (statement is CreateProcedureStatement or CreateFunctionStatement)
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

        // Only enforce SET TRANSACTION ISOLATION LEVEL for procedure/function definitions or scripts with 3+ statements
        if (!hasCreateStatement && totalStatements < 3)
        {
            yield break;
        }

        var foundTransactionIsolationLevel = false;
        var statementCount = 0;
        const int maxStatementsToCheck = 10;

        foreach (var batch in script.Batches)
        {
            foreach (var statement in batch.Statements)
            {
                statementCount++;

                // Check for SetTransactionIsolationLevelStatement
                if (statement is SetTransactionIsolationLevelStatement && statementCount <= maxStatementsToCheck)
                {
                    foundTransactionIsolationLevel = true;
                    break;
                }

                if (statementCount >= maxStatementsToCheck)
                {
                    break;
                }
            }

            if (statementCount >= maxStatementsToCheck || foundTransactionIsolationLevel)
            {
                break;
            }
        }

        if (!foundTransactionIsolationLevel)
        {
            yield return new Diagnostic(
                Range: new TsqlRefine.PluginSdk.Range(
                    new Position(0, 0),
                    new Position(0, 0)
                ),
                Message: "File should start with 'SET TRANSACTION ISOLATION LEVEL' within the first 10 statements.",
                Severity: null,
                Code: Metadata.RuleId,
                Data: new DiagnosticData(Metadata.RuleId, Metadata.Category, Metadata.Fixable)
            );
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);
}
