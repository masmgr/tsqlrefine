using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Transactions;

/// <summary>
/// Files should start with SET QUOTED_IDENTIFIER ON within the first 10 statements.
/// </summary>
public sealed class SetQuotedIdentifierRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "set-quoted-identifier",
        Description: "Files should start with SET QUOTED_IDENTIFIER ON within the first 10 statements.",
        Category: "Transactions",
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: false
    );

    public IEnumerable<Diagnostic> Analyze(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Ast.Fragment is not TSqlScript script)
        {
            yield break;
        }

        if (!ScriptStatementAnalysisHelpers.ShouldEnforcePreambleChecks(script))
        {
            yield break;
        }

        const int maxStatementsToCheck = 10;
        var foundQuotedIdentifierOn = ScriptStatementAnalysisHelpers.AnyInFirstStatements(
            script,
            maxStatementsToCheck,
            static statement => statement is PredicateSetStatement
            {
                IsOn: true,
                Options: SetOptions.QuotedIdentifier
            });

        if (!foundQuotedIdentifierOn)
        {
            yield return ScriptStatementAnalysisHelpers.CreateFileStartDiagnostic(
                Metadata,
                "File should start with 'SET QUOTED_IDENTIFIER ON' within the first 10 statements.");
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);
}
